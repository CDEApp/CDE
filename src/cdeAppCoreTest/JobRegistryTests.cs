using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Jobs;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

/// <summary>
/// The job registry over a real two-catalog session in temp folders. A rescan is kept running by holding a read
/// lease on its catalog: it then waits at its final swap (phase "swapping") until the lease is released.
/// </summary>
[TestFixture]
public class JobRegistryTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private string _dir;
    private string _cat;
    private string[] _data;
    private string[] _cdex;
    private CatalogSession _session;
    private JobRegistry _jobs;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-jobs-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        Directory.CreateDirectory(_cat);
        _data = [MakeData("d0"), MakeData("d1")];
        _cdex = [ScanToCdex(_data[0], "zero.cdex"), ScanToCdex(_data[1], "one.cdex")];

        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_cdex.ToList());
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _jobs?.Dispose();
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private string MakeData(string name)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, "a.txt"), "a");
        return d;
    }

    private string ScanToCdex(string dataDir, string cdexName)
    {
        var re = new RootEntry();
        re.PopulateRoot(dataDir);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        var path = Path.Combine(_cat, cdexName);
        re.ActualFileName = path;
        ColumnarFormat.Write(re, path);
        return path;
    }

    private JobRegistry Registry(int maxConcurrent = 3) => _jobs = new JobRegistry(_session, maxConcurrent);

    private static JobSubmitRequest Rescan(int catalogId) => new() { Kind = JobKind.Rescan, CatalogId = catalogId };

    private static JobSubmitRequest Create(string root, string fileName) =>
        new() { Kind = JobKind.Create, RootPath = root, FileName = fileName };

    private static JobDto Accepted(JobSubmission submission)
    {
        Assert.That(submission.Refusal, Is.Null, () => $"{submission.Refusal?.Code}: {submission.Refusal?.Message}");
        return submission.Job;
    }

    private string[] RootNames(int catalogId) =>
        _session.GetChildren(new EntryRefDto(catalogId, 0)).Select(c => c.Name).OrderBy(n => n).ToArray();

    private static JobDto Current(JobRegistry jobs, long id) => jobs.GetJobs().SingleOrDefault(j => j.Id == id);

    private static async Task<JobDto> WaitFor(JobRegistry jobs, long id, Func<JobDto, bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            var job = Current(jobs, id);
            if (job != null && condition(job)) return job;
            if (DateTime.UtcNow > deadline) Assert.Fail($"job {id} never got there; last seen {job}");
            await Task.Delay(10);
        }
    }

    private static Task<JobDto> WaitForEnd(JobRegistry jobs, long id) => WaitFor(jobs, id, j => j.IsTerminal);

    private static Task<JobDto> WaitAtSwap(JobRegistry jobs, long id) => WaitFor(jobs, id, j => j.Phase == "swapping");

    [Test]
    public async Task Rescans_of_two_different_catalogs_run_at_the_same_time()
    {
        var jobs = Registry();
        foreach (var d in _data) File.WriteAllText(Path.Combine(d, "new.txt"), "n");
        JobDto a, b;
        using (new CatalogHold(_session, 0))
        using (new CatalogHold(_session, 1))
        {
            a = Accepted(jobs.Submit(Rescan(0)));
            b = Accepted(jobs.Submit(Rescan(1)));

            Assert.That((await WaitAtSwap(jobs, a.Id)).State, Is.EqualTo(JobState.Running));
            Assert.That((await WaitAtSwap(jobs, b.Id)).State, Is.EqualTo(JobState.Running));
        }

        Assert.That((await WaitForEnd(jobs, a.Id)).State, Is.EqualTo(JobState.Succeeded));
        Assert.That((await WaitForEnd(jobs, b.Id)).State, Is.EqualTo(JobState.Succeeded));
        Assert.That(RootNames(0), Is.EqualTo(new[] { "a.txt", "new.txt" }));
        Assert.That(RootNames(1), Is.EqualTo(new[] { "a.txt", "new.txt" }));
    }

    [Test]
    public async Task A_second_job_for_a_catalog_is_refused_while_the_first_is_running_or_queued()
    {
        var jobs = Registry(maxConcurrent: 1);
        JobDto running, queued;
        using (new CatalogHold(_session, 0))
        {
            running = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, running.Id);
            queued = Accepted(jobs.Submit(Rescan(1)));

            Assert.That(jobs.Submit(Rescan(0)).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Assert.That(jobs.Submit(Rescan(1)).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
        }

        await WaitForEnd(jobs, running.Id);
        await WaitForEnd(jobs, queued.Id);
        Assert.That(jobs.GetJobs(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task With_a_cap_of_one_the_second_job_queues_and_starts_when_the_first_ends()
    {
        var jobs = Registry(maxConcurrent: 1);
        JobDto first, second;
        using (new CatalogHold(_session, 0))
        {
            first = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, first.Id);
            second = Accepted(jobs.Submit(Rescan(1)));
            await Task.Delay(200);

            var waiting = Current(jobs, second.Id);
            Assert.That(waiting.State, Is.EqualTo(JobState.Queued));
            Assert.That(waiting.Phase, Is.EqualTo("queued"));
            Assert.That(waiting.StartedAtUtc, Is.Null);
        }

        var firstEnded = await WaitForEnd(jobs, first.Id);
        var secondEnded = await WaitForEnd(jobs, second.Id);
        Assert.That(secondEnded.State, Is.EqualTo(JobState.Succeeded));
        Assert.That(secondEnded.StartedAtUtc, Is.GreaterThanOrEqualTo(firstEnded.EndedAtUtc));
    }

    [Test]
    public async Task Cancelling_a_queued_job_ends_it_at_once_without_running_it()
    {
        var jobs = Registry(maxConcurrent: 1);
        var bytes = File.ReadAllBytes(_cdex[1]);
        File.WriteAllText(Path.Combine(_data[1], "new.txt"), "n");
        JobDto running, queued;
        using (new CatalogHold(_session, 0))
        {
            running = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, running.Id);
            queued = Accepted(jobs.Submit(Rescan(1)));

            Assert.That(jobs.Cancel(queued.Id), Is.True);

            var cancelled = Current(jobs, queued.Id);
            Assert.That(cancelled.State, Is.EqualTo(JobState.Cancelled));
            Assert.That(cancelled.EndedAtUtc, Is.Not.Null);
        }

        await WaitForEnd(jobs, running.Id);
        Assert.That(Current(jobs, queued.Id).StartedAtUtc, Is.Null, "never started after the slot freed up");
        Assert.That(File.ReadAllBytes(_cdex[1]), Is.EqualTo(bytes));
        Assert.That(RootNames(1), Is.EqualTo(new[] { "a.txt" }));
    }

    [Test]
    public async Task Cancelling_a_running_job_leaves_its_catalog_untouched()
    {
        var jobs = Registry();
        var bytes = File.ReadAllBytes(_cdex[0]);
        File.WriteAllText(Path.Combine(_data[0], "new.txt"), "n");
        JobDto job;
        using (new CatalogHold(_session, 0))
        {
            job = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, job.Id);

            Assert.That(jobs.Cancel(job.Id), Is.True);
            Assert.That((await WaitForEnd(jobs, job.Id)).State, Is.EqualTo(JobState.Cancelled));
        }

        Assert.That(File.ReadAllBytes(_cdex[0]), Is.EqualTo(bytes));
        Assert.That(Directory.GetFiles(_cat), Is.EquivalentTo(_cdex), "no .tmp left behind");
        Assert.That(RootNames(0), Is.EqualTo(new[] { "a.txt" }));
        Assert.That(jobs.Cancel(job.Id), Is.True, "cancelling a finished job is a no-op");
        Assert.That(Current(jobs, job.Id).State, Is.EqualTo(JobState.Cancelled));
        Assert.That(jobs.Cancel(999), Is.False);
    }

    [Test]
    public async Task Finished_jobs_stay_listed_until_dismissed()
    {
        var jobs = Registry();
        var done = Accepted(jobs.Submit(Rescan(0)));
        Assert.That((await WaitForEnd(jobs, done.Id)).State, Is.EqualTo(JobState.Succeeded));

        JobDto active;
        using (new CatalogHold(_session, 1))
        {
            active = Accepted(jobs.Submit(Rescan(1)));
            await WaitAtSwap(jobs, active.Id);

            Assert.That(jobs.GetJobs().Select(j => j.Id), Is.EqualTo(new[] { done.Id, active.Id }));
            Assert.That(jobs.Dismiss(active.Id), Is.EqualTo(DismissOutcome.NotFinished));
            Assert.That(jobs.Dismiss(999), Is.EqualTo(DismissOutcome.NotFound));

            jobs.DismissFinished();
            Assert.That(jobs.GetJobs().Select(j => j.Id), Is.EqualTo(new[] { active.Id }));
        }

        await WaitForEnd(jobs, active.Id);
        Assert.That(jobs.Dismiss(active.Id), Is.EqualTo(DismissOutcome.Removed));
        Assert.That(jobs.GetJobs(), Is.Empty);
    }

    [Test]
    public async Task At_most_200_finished_jobs_are_kept_dropping_the_oldest()
    {
        // A fake session: with a real one, every submit's catalog listing waits behind the swap the held rescan queues.
        var gate = new TaskCompletionSource<RescanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobs = _jobs = new JobRegistry(FakeSession((id, _) => id == 0 ? gate.Task : Task.FromResult(new RescanResult())),
            maxConcurrent: 1);
        var cancelled = new List<long>();
        var running = Accepted(jobs.Submit(Rescan(0)));
        for (var i = 0; i < JobRegistry.MaxFinishedJobs + 1; i++)
        {
            var queued = Accepted(jobs.Submit(Rescan(1)));
            jobs.Cancel(queued.Id);
            cancelled.Add(queued.Id);
        }

        var ids = jobs.GetJobs().Select(j => j.Id).ToList();
        Assert.That(ids, Has.Count.EqualTo(JobRegistry.MaxFinishedJobs + 1), "200 finished plus the running one");
        Assert.That(ids, Does.Not.Contain(cancelled[0]));
        Assert.That(ids, Does.Contain(cancelled[^1]).And.Contain(running.Id));

        gate.SetResult(new RescanResult());
        await WaitForEnd(jobs, running.Id);
        var after = jobs.GetJobs().Select(j => j.Id).ToList();
        Assert.That(after, Has.Count.EqualTo(JobRegistry.MaxFinishedJobs));
        Assert.That(after, Does.Contain(running.Id).And.Not.Contain(cancelled[1]));
    }

    [Test]
    public async Task A_create_with_the_same_file_name_as_a_queued_create_is_refused()
    {
        var jobs = Registry(maxConcurrent: 1);
        var root = MakeData("fresh-root");
        JobDto running, create;
        using (new CatalogHold(_session, 0))
        {
            running = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, running.Id);
            create = Accepted(jobs.Submit(Create(root, "fresh")));

            Assert.That(create.State, Is.EqualTo(JobState.Queued));
            Assert.That(create.CatalogId, Is.Null);
            Assert.That(create.CatalogName, Is.EqualTo("fresh"));
            Assert.That(jobs.Submit(Create(_data[1], "fresh")).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Accepted(jobs.Submit(Create(_data[1], "other")));
        }

        var created = await WaitForEnd(jobs, create.Id);
        Assert.That(created.State, Is.EqualTo(JobState.Succeeded), () => created.Error?.Message);
        Assert.That(created.CatalogId, Is.EqualTo(created.Result.CatalogId).And.GreaterThanOrEqualTo(2));
        Assert.That(File.Exists(created.Result.SavedPath), Is.True);
        Assert.That(jobs.Submit(Create(root, "fresh")).Refusal.Code, Is.EqualTo(JobErrorCodes.FileExists),
            "once finished, the name is taken by the file, not by a job");
    }

    [Test]
    public async Task No_exclusive_hold_while_a_job_is_active_and_no_submit_while_a_hold_is_out()
    {
        var jobs = Registry();
        using (var hold = jobs.TryBeginExclusive())
        {
            Assert.That(hold, Is.Not.Null);
            Assert.That(jobs.TryBeginExclusive(), Is.Null, "one hold at a time");
            Assert.That(jobs.Submit(Rescan(0)).Refusal.Code, Is.EqualTo(JobErrorCodes.Busy));
        }

        JobDto job;
        using (new CatalogHold(_session, 0))
        {
            job = Accepted(jobs.Submit(Rescan(0)));
            Assert.That(jobs.AnyActive, Is.True);
            Assert.That(jobs.TryBeginExclusive(), Is.Null);
        }

        await WaitForEnd(jobs, job.Id);
        Assert.That(jobs.AnyActive, Is.False);
        using var after = jobs.TryBeginExclusive();
        Assert.That(after, Is.Not.Null);
    }

    [Test]
    public void A_rescan_whose_preflight_raced_a_reload_is_refused_as_busy()
    {
        var session = FakeSession(_ => new RescanResult());
        var jobs = _jobs = new JobRegistry(session);
        session.PreflightRescan(Arg.Any<int>()).Returns(_ =>
        {
            // A whole reload (which renumbers catalog ids) lands between the preflight and the enqueue.
            jobs.TryBeginExclusive().Dispose();
            return RescanPreflight.Ready;
        });

        Assert.That(jobs.Submit(Rescan(0)).Refusal?.Code, Is.EqualTo(JobErrorCodes.Busy));
        Assert.That(jobs.GetJobs(), Is.Empty);
        session.DidNotReceive().RescanAsync(Arg.Any<int>(), Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Submit_refusals_carry_the_preflight_reason()
    {
        var jobs = Registry();
        Assert.That(jobs.Submit(Rescan(9)).Refusal.Code, Is.EqualTo(JobErrorCodes.UnknownCatalog));
        Assert.That(jobs.Submit(new JobSubmitRequest()).Refusal.Code, Is.EqualTo(JobErrorCodes.InvalidRequest));
        Assert.That(jobs.Submit(Create(_data[0], @"a\b")).Refusal.Code, Is.EqualTo(JobErrorCodes.InvalidFileName));
        Assert.That(jobs.Submit(Create(_data[0], "zero")).Refusal.Code, Is.EqualTo(JobErrorCodes.FileExists));

        Directory.Delete(_data[1], true);
        var refusal = jobs.Submit(Rescan(1)).Refusal;
        Assert.That(refusal.Code, Is.EqualTo(JobErrorCodes.RootNotAccessible));
        Assert.That(refusal.RootPath, Is.EqualTo(_session.GetCatalogs()[1].RootPath));
        Assert.That(jobs.GetJobs(), Is.Empty);
    }

    [Test]
    public async Task Subscribers_get_a_snapshot_then_every_state_change_in_order()
    {
        var jobs = Registry();
        var earlier = Accepted(jobs.Submit(Rescan(0)));
        await WaitForEnd(jobs, earlier.Id);

        using var subscription = jobs.Subscribe();
        var job = Accepted(jobs.Submit(Rescan(1)));
        var seen = await ReadUntilEnd(subscription, job.Id);

        Assert.That(subscription.Snapshot.Select(j => j.Id), Is.EqualTo(new[] { earlier.Id }));
        Assert.That(DistinctStates(seen), Is.EqualTo(new[] { JobState.Queued, JobState.Running, JobState.Succeeded }));
    }

    [Test]
    public async Task Progress_is_published_at_most_every_quarter_second_but_the_job_keeps_the_highest_count()
    {
        var session = FakeSession(progress =>
        {
            for (var i = 1; i <= 1000; i++) progress.Report(new RescanProgress(0, "scanning", i, $"dir{i}"));
            progress.Report(new RescanProgress(0, "writing", 0, "root"));
            return new RescanResult { CatalogId = 0, Entries = 1000 };
        });
        _jobs = new JobRegistry(session);
        using var subscription = _jobs.Subscribe();

        var job = Accepted(_jobs.Submit(Rescan(0)));
        var seen = await ReadUntilEnd(subscription, job.Id);

        var running = seen.Where(j => j.State == JobState.Running).ToList();
        Assert.That(running, Has.Count.LessThanOrEqualTo(4), "start, writing, and at most a couple of throttled reports");
        Assert.That(running.Last().Phase, Is.EqualTo("writing"));
        Assert.That(running.Last().EntriesScanned, Is.EqualTo(1000), "writing reports 0 but the count is kept");
        Assert.That(seen.Last().State, Is.EqualTo(JobState.Succeeded));
        Assert.That(seen.Last().Result.Entries, Is.EqualTo(1000));
    }

    [Test]
    public async Task A_job_that_throws_fails_with_a_code_and_is_logged()
    {
        var boom = new InvalidOperationException("boom");
        var logger = Substitute.For<ILogger>();
        _jobs = new JobRegistry(FakeSession(_ => throw boom), logger: logger);

        var job = Accepted(_jobs.Submit(Rescan(0)));
        var failed = await WaitForEnd(_jobs, job.Id);

        Assert.That(failed.State, Is.EqualTo(JobState.Failed));
        Assert.That(failed.Error.Code, Is.EqualTo(JobErrorCodes.RescanFailed));
        Assert.That(failed.Error.Message, Is.EqualTo("boom"));
        logger.Received(1).Error(boom, Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>());
    }

    [Test]
    public void Terminal_states_are_final()
    {
        var job = new JobDto { Id = 1, State = JobState.Queued }.Cancel(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => job.Start(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => job.Fail(new JobError(), DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => new JobDto { State = JobState.Queued }.Succeed(new JobResult(), DateTime.UtcNow));
    }

    private static ICatalogSession FakeSession(Func<IProgress<RescanProgress>, RescanResult> rescan) =>
        FakeSession((_, progress) => Task.FromResult(rescan(progress)));

    /// <summary>Two catalogs (0 and 1) that always pass the preflight; each rescan runs <paramref name="rescan"/>.</summary>
    private static ICatalogSession FakeSession(Func<int, IProgress<RescanProgress>, Task<RescanResult>> rescan)
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightRescan(Arg.Any<int>()).Returns(RescanPreflight.Ready);
        session.GetCatalogs().Returns([
            new CatalogInfoDto { CatalogId = 0, RootPath = "root0", SourcePath = Path.Combine(Path.GetTempPath(), "fake0.cdex") },
            new CatalogInfoDto { CatalogId = 1, RootPath = "root1", SourcePath = Path.Combine(Path.GetTempPath(), "fake1.cdex") }
        ]);
        session.RescanAsync(Arg.Any<int>(), Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>())
            .Returns(ci => rescan(ci.ArgAt<int>(0), ci.Arg<IProgress<RescanProgress>>()));
        return session;
    }

    private static async Task<List<JobDto>> ReadUntilEnd(JobSubscription subscription, long id)
    {
        using var cts = new CancellationTokenSource(Timeout);
        var seen = new List<JobDto>();
        await foreach (var job in subscription.Changes.ReadAllAsync(cts.Token))
        {
            if (job.Id != id) continue;
            seen.Add(job);
            if (job.IsTerminal) break;
        }

        return seen;
    }

    private static JobState[] DistinctStates(IEnumerable<JobDto> jobs)
    {
        var states = new List<JobState>();
        foreach (var job in jobs)
            if (states.Count == 0 || states[^1] != job.State) states.Add(job.State);
        return states.ToArray();
    }

    /// <summary>Holds a read lease on one catalog (on its own thread, as the lock requires) until disposed.</summary>
    private sealed class CatalogHold : IDisposable
    {
        private readonly ManualResetEventSlim _acquired = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Task _holder;

        public CatalogHold(ICatalogSession session, int catalogId)
        {
            _holder = Task.Factory.StartNew(() =>
            {
                using var lease = session.AcquireRead(catalogId);
                _acquired.Set();
                _release.Wait();
            }, TaskCreationOptions.LongRunning);
            _acquired.Wait();
        }

        public void Dispose()
        {
            _release.Set();
            _holder.Wait();
        }
    }
}
