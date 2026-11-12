using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Hashing;
using Serilog;

namespace cdeAppCore.Jobs;

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
/// Owns the catalog jobs (rescan, create, hash) of one session. At most <see cref="MaxConcurrent"/> run at once and the
/// rest wait in submit order. A catalog file is the target of at most one queued or running job. Finished jobs stay
/// listed until dismissed (at most <see cref="MaxFinishedJobs"/>, oldest dropped).
/// </summary>
public sealed class JobRegistry : IDisposable
{
    public const int MaxFinishedJobs = 200;

    /// <summary>
    /// Changes a subscriber may fall behind by. One that falls further is ended, so its client reconnects and starts
    /// again from a snapshot instead of the sidecar buffering every progress event for it.
    /// </summary>
    public const int MaxSubscriberBacklog = 1000;
    private const long ProgressIntervalMs = 250;

    private readonly ICatalogSession _session;
    private readonly ILogger _logger;

    private readonly object _lock = new();
    private readonly List<Entry> _jobs = [];
    private readonly List<Channel<JobDto>> _subscribers = [];
    private long _lastId;
    private int _running;
    private bool _exclusive;
    private long _exclusiveGeneration;
    private bool _disposed;

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
        JobKind.Hash => SubmitHash(request),
        _ => JobSubmission.Refused(JobErrorCodes.InvalidRequest, "kind must be \"rescan\", \"create\" or \"hash\".")
    };

    private long ExclusiveGeneration()
    {
        lock (_lock) return _exclusiveGeneration;
    }

    private JobSubmission SubmitRescan(int catalogId)
    {
        var generation = ExclusiveGeneration();
        var preflight = _session.PreflightRescan(catalogId);
        if (preflight != RescanPreflight.Ready) return RefuseRescan(catalogId, preflight);

        var info = _session.GetCatalogs().FirstOrDefault(c => c.CatalogId == catalogId);
        if (info == null) return RefuseRescan(catalogId, RescanPreflight.Unavailable);

        var target = Path.ChangeExtension(Path.GetFullPath(info.SourcePath), CatalogNaming.Extension);
        return Enqueue(generation, [target], new JobDto
        {
            Kind = JobKind.Rescan,
            Title = $"Rescanning {info.RootPath}",
            CatalogId = catalogId,
            CatalogName = Path.GetFileNameWithoutExtension(info.SourcePath)
        });
    }

    private JobSubmission SubmitHash(JobSubmitRequest request)
    {
        var generation = ExclusiveGeneration();
        if (_session.PreflightHash(request.CatalogIds, request.Hash) is { } refusal)
            return new JobSubmission(null, refusal);

        var ids = request.CatalogIds.Distinct().Order().ToArray();
        var catalogs = _session.GetCatalogs().Where(c => ids.Contains(c.CatalogId)).ToList();
        if (catalogs.Count != ids.Length)
            return JobSubmission.Refused(JobErrorCodes.CatalogUnavailable, "A catalog became unavailable; reload catalogs.");

        var names = catalogs.Select(c => Path.GetFileNameWithoutExtension(c.SourcePath)).ToList();
        return Enqueue(generation, catalogs.Select(c => Path.GetFullPath(c.SourcePath)).ToArray(), new JobDto
        {
            Kind = JobKind.Hash,
            Title = names.Count == 1 ? $"Hashing {names[0]}" : $"Hashing {names.Count} catalogs",
            CatalogIds = ids,
            CatalogName = string.Join(", ", names)
        }, hash: request.Hash ?? new HashOptions());
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
        var generation = ExclusiveGeneration();
        if (request.Hash != null && ValidateHashFilter(request.Hash) is { } badFilter) return new JobSubmission(null, badFilter);

        var pf = _session.PreflightCreate(new CreatePreflightRequest { RootPath = request.RootPath, FileName = request.FileName });

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

        return Enqueue(generation, [pf.TargetPath], new JobDto
            {
                Kind = JobKind.Create,
                Title = $"Creating catalog {pf.FileName}",
                CatalogName = pf.FileName
            },
            new CreateCatalogRequest
            {
                RootPath = request.RootPath,
                FileName = pf.FileName,
                Description = request.Description,
                FollowJunctions = request.FollowJunctions
            }, request.Hash);
    }

    private static JobError ValidateHashFilter(HashOptions options)
    {
        try
        {
            HashSelection.Parse(options.Filter, options.IncludeUniqueSizes);
            return null;
        }
        catch (cdeLib.EverythingQueryException ex)
        {
            return new JobError { Code = JobErrorCodes.InvalidFilter, Message = ex.Message };
        }
    }

    private static JobSubmission AlreadyRunning(string what) =>
        JobSubmission.Refused(JobErrorCodes.AlreadyRunning, $"A job for {what} is already queued or running.");

    private static JobSubmission Busy() =>
        JobSubmission.Refused(JobErrorCodes.Busy, "Catalogs are being loaded or migrated; try again when that finishes.");

    private static JobSubmission RootNotAccessible(string rootPath) =>
        JobSubmission.Refused(JobErrorCodes.RootNotAccessible, new RootNotAccessibleException(rootPath).Message, rootPath);

    private JobSubmission Enqueue(long generationAtPreflight, string[] targets, JobDto job,
        CreateCatalogRequest create = null, HashOptions hash = null)
    {
        lock (_lock)
        {
            if (_disposed || _exclusive || generationAtPreflight != _exclusiveGeneration) return Busy();
            foreach (var target in targets)
            {
                if (IsActiveTargetLocked(target)) return AlreadyRunning(Path.GetFileNameWithoutExtension(target));
            }

            var entry = new Entry(targets, create, hash)
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
            PublishLocked(entry.Job);
            StartQueuedJobsLocked();
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
                FinishLocked(entry, entry.Job.Cancel(DateTime.UtcNow));
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
        var channel = Channel.CreateBounded<JobDto>(new BoundedChannelOptions(MaxSubscriberBacklog)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        IReadOnlyList<JobDto> snapshot;
        lock (_lock)
        {
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
            _exclusiveGeneration++;
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

    private void StartQueuedJobsLocked()
    {
        if (_disposed) return;
        foreach (var entry in _jobs)
        {
            if (_running >= MaxConcurrent) return;
            if (entry.Job.State != JobState.Queued) continue;
            _running++;
            UpdateLocked(entry, entry.Job.Start(DateTime.UtcNow));
            _ = Task.Run(() => RunAsync(entry));
        }
    }

    private async Task RunAsync(Entry entry)
    {
        Func<JobDto, JobDto> end;
        try
        {
            var (result, note) = await ExecuteAsync(entry).ConfigureAwait(false);
            end = job => job.Succeed(result, DateTime.UtcNow, note);
        }
        catch (OperationCanceledException) when (entry.Cts.IsCancellationRequested)
        {
            end = job => job.Cancel(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var (error, unexpected) = ToError(entry.Job.Kind, ex);
            if (unexpected) _logger?.Error(ex, "Job {JobId} ({Title}) failed", entry.Job.Id, entry.Job.Title);
            end = job => job.Fail(error, DateTime.UtcNow);
        }

        lock (_lock)
        {
            _running--;
            FinishLocked(entry, end(entry.Job));
            StartQueuedJobsLocked();
        }
    }

    private async Task<(JobResult Result, string Note)> ExecuteAsync(Entry entry)
    {
        var ct = entry.Cts.Token;
        if (entry.Job.Kind == JobKind.Hash)
        {
            var hashed = await _session.HashAsync(entry.Job.CatalogIds, entry.Hash,
                new ProgressSink<HashProgress>(p => OnProgress(entry, p.Phase, 0, p.Note, p.Counts)), ct)
                .ConfigureAwait(false);
            return (new JobResult { Hash = hashed }, null);
        }

        if (entry.Job.Kind == JobKind.Rescan)
        {
            var r = await _session.RescanAsync(entry.Job.CatalogId!.Value,
                new ProgressSink<RescanProgress>(p => OnProgress(entry, p.Phase, p.EntriesScanned, p.CurrentPath)), ct)
                .ConfigureAwait(false);
            return (new JobResult
            {
                CatalogId = r.CatalogId, RootPath = r.RootPath, Entries = r.Entries, SavedPath = r.SavedPath,
                ExcludedFolderCount = r.ExcludedFolderCount, AccessDeniedFolderCount = r.AccessDeniedFolderCount,
                UnreadableFolderCount = r.UnreadableFolderCount
            }, null);
        }

        var c = await _session.CreateCatalogAsync(entry.Create,
            new ProgressSink<CreateProgress>(p => OnProgress(entry, p.Phase, p.EntriesScanned, p.CurrentPath)), ct)
            .ConfigureAwait(false);
        var created = new JobResult
        {
            CatalogId = c.CatalogId, RootPath = c.RootPath, Entries = c.Entries, SavedPath = c.SavedPath,
            ExcludedFolderCount = c.ExcludedFolderCount, AccessDeniedFolderCount = c.AccessDeniedFolderCount,
            UnreadableFolderCount = c.UnreadableFolderCount
        };
        return entry.Hash == null ? (created, null) : await HashCreatedAsync(entry, created).ConfigureAwait(false);
    }

    // The scan is already saved and in the session, so a stop or failure here leaves a created catalog without hashes.
    private async Task<(JobResult Result, string Note)> HashCreatedAsync(Entry entry, JobResult created)
    {
        try
        {
            created.Hash = await _session.HashAsync([created.CatalogId!.Value], entry.Hash,
                new ProgressSink<HashProgress>(p => OnProgress(entry, p.Phase, 0, p.Note, p.Counts)), entry.Cts.Token)
                .ConfigureAwait(false);
            return (created, null);
        }
        catch (OperationCanceledException) when (entry.Cts.IsCancellationRequested)
        {
            return (created, "Hashing cancelled");
        }
        catch (Exception ex)
        {
            if (ex is not HashRefusedException) _logger?.Error(ex, "Hashing the new catalog of job {JobId} failed", entry.Job.Id);
            return (created, $"Hashing failed: {ex.Message}");
        }
    }

    private static (JobError Error, bool Unexpected) ToError(JobKind kind, Exception ex)
    {
        if (ex is HashRefusedException refused) return (refused.Error, false);
        if (ex is HashFailedException) return (new JobError { Code = JobErrorCodes.HashFailed, Message = ex.Message }, true);

        var knownCode = ex switch
        {
            RootNotAccessibleException => JobErrorCodes.RootNotAccessible,
            NoSourceFileException => JobErrorCodes.NoSourceFile,
            CatalogFileExistsException => JobErrorCodes.FileExists,
            RescanAlreadyRunningException => JobErrorCodes.AlreadyRunning,
            CatalogJobBusyException => JobErrorCodes.Busy,
            InvalidCatalogFileNameException => JobErrorCodes.InvalidFileName,
            CatalogFolderNotWritableException => JobErrorCodes.FolderNotWritable,
            NoCatalogDirException => JobErrorCodes.NoCatalogDir,
            _ => null
        };
        var error = new JobError
        {
            Code = knownCode ?? kind switch
            {
                JobKind.Rescan => JobErrorCodes.RescanFailed,
                JobKind.Create => JobErrorCodes.CreateFailed,
                _ => JobErrorCodes.HashFailed
            },
            Message = ex.Message,
            RootPath = (ex as RootNotAccessibleException)?.RootPath
        };
        return (error, knownCode == null);
    }

    private void OnProgress(Entry entry, string phase, int entriesScanned, string note, HashJobProgress? hash = null)
    {
        lock (_lock)
        {
            if (entry.Job.State != JobState.Running) return;
            var phaseChanged = phase != entry.Job.Phase;
            entry.Job = entry.Job with
            {
                Phase = phase,
                EntriesScanned = Math.Max(entry.Job.EntriesScanned, entriesScanned),
                Note = note,
                Hash = hash
            };
            var now = Environment.TickCount64;
            if (!phaseChanged && now - entry.LastPublishedMs < ProgressIntervalMs) return;
            entry.LastPublishedMs = now;
            PublishLocked(entry.Job);
        }
    }

    private void UpdateLocked(Entry entry, JobDto job)
    {
        entry.Job = job;
        entry.LastPublishedMs = Environment.TickCount64;
        PublishLocked(job);
    }

    private void FinishLocked(Entry entry, JobDto terminal)
    {
        UpdateLocked(entry, terminal);
        var finished = _jobs.Where(e => e.Job.IsTerminal).ToList();
        foreach (var old in finished.OrderBy(e => e.Job.EndedAtUtc).ThenBy(e => e.Job.Id)
                     .Take(finished.Count - MaxFinishedJobs))
        {
            _jobs.Remove(old);
        }
    }

    private void PublishLocked(JobDto job)
    {
        for (var i = _subscribers.Count - 1; i >= 0; i--)
        {
            var subscriber = _subscribers[i];
            if (subscriber.Writer.TryWrite(job)) continue;
            _subscribers.RemoveAt(i);
            subscriber.Writer.TryComplete();
        }
    }

    private bool IsActiveTarget(string target)
    {
        lock (_lock) return IsActiveTargetLocked(target);
    }

    private bool IsActiveTargetLocked(string target) =>
        _jobs.Any(e => !e.Job.IsTerminal && e.TargetFilePaths.Contains(target, PathComparer));

    private bool AnyActiveLocked() => _jobs.Any(e => !e.Job.IsTerminal);

    private Entry Find(long id) => _jobs.Find(e => e.Job.Id == id);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Asks running jobs to stop, starts no queued job and accepts no submit, and ends every subscription.</summary>
    public void Dispose()
    {
        List<Entry> running;
        List<Channel<JobDto>> subscribers;
        lock (_lock)
        {
            _disposed = true;
            running = _jobs.Where(e => e.Job.State == JobState.Running).ToList();
            subscribers = [.. _subscribers];
            _subscribers.Clear();
        }

        foreach (var entry in running) entry.Cts.Cancel();
        foreach (var subscriber in subscribers) subscriber.Writer.TryComplete();
    }

    private sealed class Entry(string[] targets, CreateCatalogRequest create, HashOptions hash)
    {
        public string[] TargetFilePaths { get; } = targets;

        public CreateCatalogRequest Create { get; } = create;
        public HashOptions Hash { get; } = hash;
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
