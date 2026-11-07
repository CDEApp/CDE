using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using Serilog;

namespace cdeAppCore.Jobs;

/// <summary>The outcome of <see cref="JobRegistry.Submit"/>: the accepted job, or why it was refused.</summary>
public sealed record JobSubmission(JobDto Job, JobError Refusal)
{
    public static JobSubmission Accepted(JobDto job) => new(job, null);
    public static JobSubmission Refused(string code, string message, string rootPath = null) =>
        new(null, new JobError { Code = code, Message = message, RootPath = rootPath });
}

public enum DismissOutcome
{
    Removed,
    NotFound,
    NotFinished
}

/// <summary>A snapshot of every job plus every change after it, in order. Dispose to stop receiving.</summary>
public sealed class JobSubscription(IReadOnlyList<JobDto> snapshot, ChannelReader<JobDto> changes, Action unsubscribe)
    : IDisposable
{
    public IReadOnlyList<JobDto> Snapshot { get; } = snapshot;
    public ChannelReader<JobDto> Changes { get; } = changes;
    public void Dispose() => unsubscribe();
}

/// <summary>
/// Owns the catalog scan jobs (rescan, create) of one session. At most <see cref="MaxConcurrent"/> run at once and the
/// rest wait in submit order. A catalog file is the target of at most one queued or running job. Finished jobs stay
/// listed until dismissed (at most <see cref="MaxFinishedJobs"/>, oldest dropped). The work itself runs through
/// <see cref="ICatalogSession.RescanAsync"/> and <see cref="ICatalogSession.CreateCatalogAsync"/>.
/// </summary>
public sealed class JobRegistry : IDisposable
{
    public const int MaxFinishedJobs = 200;
    private const long ProgressIntervalMs = 250;

    private readonly ICatalogSession _session;
    private readonly ILogger _logger;

    // Guards everything below. Never held across session calls that scan or do file IO.
    private readonly object _lock = new();
    private readonly List<Entry> _jobs = [];
    private readonly List<Channel<JobDto>> _subscribers = [];
    private long _lastId;
    private int _running;
    private bool _exclusive;

    public JobRegistry(ICatalogSession session, int maxConcurrent = 3, ILogger logger = null)
    {
        _session = session;
        _logger = logger;
        MaxConcurrent = Math.Max(1, maxConcurrent);
    }

    public int MaxConcurrent { get; }

    /// <summary>Whether any job is queued or running.</summary>
    public bool AnyActive
    {
        get { lock (_lock) return AnyActiveLocked(); }
    }

    public IReadOnlyList<JobDto> GetJobs()
    {
        lock (_lock) return _jobs.Select(e => e.Job).ToList();
    }

    public JobSubmission Submit(JobSubmitRequest request) => request?.Kind switch
    {
        JobKind.Rescan when request.CatalogId is { } id => SubmitRescan(id),
        JobKind.Rescan => JobSubmission.Refused(JobErrorCodes.InvalidRequest, "A rescan needs a catalogId."),
        JobKind.Create => SubmitCreate(request),
        _ => JobSubmission.Refused(JobErrorCodes.InvalidRequest, "kind must be \"rescan\" or \"create\".")
    };

    private JobSubmission SubmitRescan(int catalogId)
    {
        var preflight = _session.PreflightRescan(catalogId);
        if (preflight != RescanPreflight.Ready) return RefuseRescan(catalogId, preflight);

        var info = _session.GetCatalogs().FirstOrDefault(c => c.CatalogId == catalogId);
        if (info == null) return RefuseRescan(catalogId, RescanPreflight.Unavailable);

        // A legacy .cde is rescanned into the .cdex beside it, so that is the file this job writes.
        var target = Path.ChangeExtension(Path.GetFullPath(info.SourcePath), CatalogNaming.Extension);
        return Enqueue(target, new JobDto
        {
            Kind = JobKind.Rescan,
            Title = $"Rescanning {info.RootPath}",
            CatalogId = catalogId,
            CatalogName = Path.GetFileNameWithoutExtension(info.SourcePath)
        }, null);
    }

    private JobSubmission RefuseRescan(int catalogId, RescanPreflight preflight) => preflight switch
    {
        RescanPreflight.UnknownCatalog => JobSubmission.Refused(JobErrorCodes.UnknownCatalog, $"No catalog {catalogId}."),
        RescanPreflight.AlreadyRunning => AlreadyRunning($"catalog {catalogId}"),
        RescanPreflight.Busy => Busy(),
        RescanPreflight.Unavailable => JobSubmission.Refused(JobErrorCodes.CatalogUnavailable,
            $"Catalog {catalogId} is unavailable after a failed rescan; reload catalogs."),
        RescanPreflight.NoSourceFile => JobSubmission.Refused(JobErrorCodes.NoSourceFile,
            $"Catalog {catalogId} has no source file to save a rescan to."),
        _ => RootNotAccessible(_session.GetCatalogs().FirstOrDefault(c => c.CatalogId == catalogId)?.RootPath)
    };

    private JobSubmission SubmitCreate(JobSubmitRequest request)
    {
        var pf = _session.PreflightCreate(new CreatePreflightRequest { RootPath = request.RootPath, FileName = request.FileName });

        // A running create's target reads as Exists in the preflight; report it as the job it is.
        if (pf.TargetPath != null && IsActiveTarget(pf.TargetPath)) return AlreadyRunning(pf.FileName);
        if (pf.Busy) return Busy();
        if (pf.FileNameError != null) return JobSubmission.Refused(JobErrorCodes.InvalidFileName, pf.FileNameError);
        if (pf.CatalogDir == null) return JobSubmission.Refused(JobErrorCodes.NoCatalogDir, new NoCatalogDirException().Message);
        if (!pf.RootAccessible) return RootNotAccessible(request.RootPath);
        if (!pf.Writable)
            return JobSubmission.Refused(JobErrorCodes.FolderNotWritable,
                new CatalogFolderNotWritableException(pf.CatalogDir).Message);
        if (pf.Exists)
            return JobSubmission.Refused(JobErrorCodes.FileExists,
                new CatalogFileExistsException(pf.TargetPath, pf.ExistingCatalogId).Message);

        return Enqueue(pf.TargetPath, new JobDto
            {
                Kind = JobKind.Create,
                Title = $"Creating catalog {pf.FileName}",
                CatalogName = pf.FileName
            },
            // The name resolved now (typed or default) is the one the exclusion was checked against.
            new CreateCatalogRequest
            {
                RootPath = request.RootPath,
                FileName = pf.FileName,
                Description = request.Description,
                FollowJunctions = request.FollowJunctions
            });
    }

    private static JobSubmission AlreadyRunning(string what) =>
        JobSubmission.Refused(JobErrorCodes.AlreadyRunning, $"A job for {what} is already queued or running.");

    private static JobSubmission Busy() =>
        JobSubmission.Refused(JobErrorCodes.Busy, "Catalogs are being loaded or migrated; try again when that finishes.");

    private static JobSubmission RootNotAccessible(string rootPath) =>
        JobSubmission.Refused(JobErrorCodes.RootNotAccessible, new RootNotAccessibleException(rootPath).Message, rootPath);

    private JobSubmission Enqueue(string target, JobDto job, CreateCatalogRequest create)
    {
        lock (_lock)
        {
            // Re-checked here: the preflight ran outside the lock and another submit may have taken the target since.
            if (_exclusive) return Busy();
            if (IsActiveTargetLocked(target)) return AlreadyRunning(job.CatalogName);

            var entry = new Entry(target, create)
            {
                Job = job with
                {
                    Id = ++_lastId,
                    State = JobState.Queued,
                    Phase = "queued",
                    QueuedAtUtc = DateTime.UtcNow
                }
            };
            _jobs.Add(entry);
            Publish(entry.Job);
            StartQueuedJobs();
            return JobSubmission.Accepted(entry.Job);
        }
    }

    /// <summary>
    /// Cancel a job: a queued one is cancelled at once, a running one is asked to stop (it ends as cancelled unless it
    /// had already committed), a finished one is left as it is. False when there is no such job.
    /// </summary>
    public bool Cancel(long id)
    {
        Entry running;
        lock (_lock)
        {
            var entry = Find(id);
            if (entry == null) return false;
            if (entry.Job.State == JobState.Queued)
            {
                Finish(entry, entry.Job.Cancel(DateTime.UtcNow));
                return true;
            }

            running = entry.Job.State == JobState.Running ? entry : null;
        }

        // Outside the lock: cancellation callbacks run synchronously on this thread.
        running?.Cts.Cancel();
        return true;
    }

    public DismissOutcome Dismiss(long id)
    {
        lock (_lock)
        {
            var entry = Find(id);
            if (entry == null) return DismissOutcome.NotFound;
            if (!entry.Job.IsTerminal) return DismissOutcome.NotFinished;
            _jobs.Remove(entry);
            return DismissOutcome.Removed;
        }
    }

    public void DismissFinished()
    {
        lock (_lock) _jobs.RemoveAll(e => e.Job.IsTerminal);
    }

    public JobSubscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<JobDto>(new UnboundedChannelOptions { SingleReader = true });
        IReadOnlyList<JobDto> snapshot;
        lock (_lock)
        {
            // Under the lock, so no change is missed between the snapshot and the first event.
            _subscribers.Add(channel);
            snapshot = _jobs.Select(e => e.Job).ToList();
        }

        return new JobSubscription(snapshot, channel.Reader, () =>
        {
            lock (_lock) _subscribers.Remove(channel);
            channel.Writer.TryComplete();
        });
    }

    /// <summary>
    /// For a reload or a migration, which change catalog ids: returns a hold that refuses every submit until disposed,
    /// or null while any job is queued or running (queued rescans hold catalog ids) or another hold is out.
    /// </summary>
    public IDisposable TryBeginExclusive()
    {
        lock (_lock)
        {
            if (_exclusive || AnyActiveLocked()) return null;
            _exclusive = true;
        }

        return new ExclusiveHold(this);
    }

    private sealed class ExclusiveHold(JobRegistry owner) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            lock (owner._lock) owner._exclusive = false;
        }
    }

    // Called with the lock held.
    private void StartQueuedJobs()
    {
        foreach (var entry in _jobs)
        {
            if (_running >= MaxConcurrent) return;
            if (entry.Job.State != JobState.Queued) continue;
            _running++;
            Update(entry, entry.Job.Start(DateTime.UtcNow));
            _ = Task.Run(() => RunAsync(entry));
        }
    }

    private async Task RunAsync(Entry entry)
    {
        Func<JobDto, JobDto> end;
        try
        {
            var result = await ExecuteAsync(entry).ConfigureAwait(false);
            end = job => job.Succeed(result, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (entry.Cts.IsCancellationRequested)
        {
            end = job => job.Cancel(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var error = ToError(entry.Job.Kind, ex);
            if (error.Code is JobErrorCodes.RescanFailed or JobErrorCodes.CreateFailed)
                _logger?.Error(ex, "Job {JobId} ({Title}) failed", entry.Job.Id, entry.Job.Title);
            end = job => job.Fail(error, DateTime.UtcNow);
        }

        lock (_lock)
        {
            _running--;
            Finish(entry, end(entry.Job));
            StartQueuedJobs();
        }
    }

    private async Task<JobResult> ExecuteAsync(Entry entry)
    {
        var ct = entry.Cts.Token;
        if (entry.Job.Kind == JobKind.Rescan)
        {
            var r = await _session.RescanAsync(entry.Job.CatalogId!.Value,
                new ProgressSink<RescanProgress>(p => OnProgress(entry, p.Phase, p.EntriesScanned, p.CurrentPath)), ct)
                .ConfigureAwait(false);
            return new JobResult
            {
                CatalogId = r.CatalogId, RootPath = r.RootPath, Entries = r.Entries, SavedPath = r.SavedPath,
                ExcludedFolderCount = r.ExcludedFolderCount, AccessDeniedFolderCount = r.AccessDeniedFolderCount,
                UnreadableFolderCount = r.UnreadableFolderCount
            };
        }

        var c = await _session.CreateCatalogAsync(entry.Create,
            new ProgressSink<CreateProgress>(p => OnProgress(entry, p.Phase, p.EntriesScanned, p.CurrentPath)), ct)
            .ConfigureAwait(false);
        return new JobResult
        {
            CatalogId = c.CatalogId, RootPath = c.RootPath, Entries = c.Entries, SavedPath = c.SavedPath,
            ExcludedFolderCount = c.ExcludedFolderCount, AccessDeniedFolderCount = c.AccessDeniedFolderCount,
            UnreadableFolderCount = c.UnreadableFolderCount
        };
    }

    // The session's documented refusals keep their submit-time codes (a race the preflight could not see); anything
    // else is an unexpected failure, which RunAsync logs.
    private static JobError ToError(JobKind kind, Exception ex) => new()
    {
        Code = ex switch
        {
            RootNotAccessibleException => JobErrorCodes.RootNotAccessible,
            NoSourceFileException => JobErrorCodes.NoSourceFile,
            CatalogFileExistsException => JobErrorCodes.FileExists,
            RescanAlreadyRunningException => JobErrorCodes.AlreadyRunning,
            CatalogJobBusyException => JobErrorCodes.Busy,
            InvalidCatalogFileNameException => JobErrorCodes.InvalidFileName,
            CatalogFolderNotWritableException => JobErrorCodes.FolderNotWritable,
            NoCatalogDirException => JobErrorCodes.NoCatalogDir,
            _ => kind == JobKind.Rescan ? JobErrorCodes.RescanFailed : JobErrorCodes.CreateFailed
        },
        Message = ex.Message,
        RootPath = (ex as RootNotAccessibleException)?.RootPath
    };

    // Every report updates the job; subscribers hear of it on a phase change or at most every ProgressIntervalMs.
    private void OnProgress(Entry entry, string phase, int entriesScanned, string note)
    {
        lock (_lock)
        {
            if (entry.Job.State != JobState.Running) return;
            var phaseChanged = phase != entry.Job.Phase;
            entry.Job = entry.Job with
            {
                Phase = phase,
                EntriesScanned = Math.Max(entry.Job.EntriesScanned, entriesScanned),
                Note = note
            };
            var now = Environment.TickCount64;
            if (!phaseChanged && now - entry.LastPublishedMs < ProgressIntervalMs) return;
            entry.LastPublishedMs = now;
            Publish(entry.Job);
        }
    }

    // Called with the lock held.
    private void Update(Entry entry, JobDto job)
    {
        entry.Job = job;
        entry.LastPublishedMs = Environment.TickCount64;
        Publish(job);
    }

    // Called with the lock held.
    private void Finish(Entry entry, JobDto terminal)
    {
        Update(entry, terminal);
        var finished = _jobs.Where(e => e.Job.IsTerminal).ToList();
        foreach (var old in finished.OrderBy(e => e.Job.EndedAtUtc).ThenBy(e => e.Job.Id)
                     .Take(finished.Count - MaxFinishedJobs))
        {
            _jobs.Remove(old);
        }
    }

    // Called with the lock held. Unbounded channels never refuse a write while open.
    private void Publish(JobDto job)
    {
        foreach (var subscriber in _subscribers) subscriber.Writer.TryWrite(job);
    }

    private bool IsActiveTarget(string target)
    {
        lock (_lock) return IsActiveTargetLocked(target);
    }

    private bool IsActiveTargetLocked(string target) =>
        _jobs.Any(e => !e.Job.IsTerminal && PathComparer.Equals(e.Target, target));

    private bool AnyActiveLocked() => _jobs.Any(e => !e.Job.IsTerminal);

    private Entry Find(long id) => _jobs.Find(e => e.Job.Id == id);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Asks running jobs to stop and ends every subscription.</summary>
    public void Dispose()
    {
        List<Entry> running;
        List<Channel<JobDto>> subscribers;
        lock (_lock)
        {
            running = _jobs.Where(e => e.Job.State == JobState.Running).ToList();
            subscribers = [.. _subscribers];
            _subscribers.Clear();
        }

        foreach (var entry in running) entry.Cts.Cancel();
        foreach (var subscriber in subscribers) subscriber.Writer.TryComplete();
    }

    private sealed class Entry(string target, CreateCatalogRequest create)
    {
        /// <summary>Full path of the catalog file the job writes: the exclusion key.</summary>
        public string Target { get; } = target;

        public CreateCatalogRequest Create { get; } = create;
        public CancellationTokenSource Cts { get; } = new();
        public JobDto Job { get; set; }
        public long LastPublishedMs { get; set; }
    }

    // Reports synchronously on the scanning thread; Progress<T> would post them out of order.
    private sealed class ProgressSink<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
