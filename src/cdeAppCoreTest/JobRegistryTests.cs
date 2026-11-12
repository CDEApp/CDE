using cdeLib.Hashing;
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
        loader.GetColumnarFiles(Arg.Any<string>())
            .Returns(_ => _cdex.Concat(Directory.GetFiles(_cat, "*.cdex").Except(_cdex)).ToList());
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _jobs?.Dispose();
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { }
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
        using (new SwapBlockingLease(_session, 0))
        using (new SwapBlockingLease(_session, 1))
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
        using (new SwapBlockingLease(_session, 0))
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
        using (new SwapBlockingLease(_session, 0))
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
        using (new SwapBlockingLease(_session, 0))
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
        using (new SwapBlockingLease(_session, 0))
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
        using (new SwapBlockingLease(_session, 1))
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
    public async Task A_subscriber_that_stops_reading_is_ended_instead_of_buffering_without_limit()
    {
        var gate = new TaskCompletionSource<RescanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobs = _jobs = new JobRegistry(FakeSession((id, _) => id == 0 ? gate.Task : Task.FromResult(new RescanResult())),
            maxConcurrent: 1);
        using var stalled = jobs.Subscribe();
        Accepted(jobs.Submit(Rescan(0)));
        for (var i = 0; i < JobRegistry.MaxSubscriberBacklog; i++) jobs.Cancel(Accepted(jobs.Submit(Rescan(1))).Id);

        var buffered = 0;
        using var cts = new CancellationTokenSource(Timeout);
        await foreach (var _ in stalled.Changes.ReadAllAsync(cts.Token)) buffered++;
        Assert.That(buffered, Is.EqualTo(JobRegistry.MaxSubscriberBacklog));

        using var fresh = jobs.Subscribe();
        Assert.That(fresh.Snapshot, Is.Not.Empty);
        gate.SetResult(new RescanResult());
    }

    [Test]
    public async Task At_most_200_finished_jobs_are_kept_dropping_the_oldest()
    {
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
        using (new SwapBlockingLease(_session, 0))
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
        using (new SwapBlockingLease(_session, 0))
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
            jobs.TryBeginExclusive().Dispose();
            return RescanPreflight.Ready;
        });

        Assert.That(jobs.Submit(Rescan(0)).Refusal?.Code, Is.EqualTo(JobErrorCodes.Busy));
        Assert.That(jobs.GetJobs(), Is.Empty);
        session.DidNotReceive().RescanAsync(Arg.Any<int>(), Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task After_dispose_no_queued_job_starts_and_no_submit_is_accepted()
    {
        var gate = new TaskCompletionSource<RescanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = FakeSession((id, _) => id == 0 ? gate.Task : Task.FromResult(new RescanResult()));
        var jobs = _jobs = new JobRegistry(session, maxConcurrent: 1);
        var running = Accepted(jobs.Submit(Rescan(0)));
        var queued = Accepted(jobs.Submit(Rescan(1)));

        jobs.Dispose();
        gate.SetResult(new RescanResult());
        await WaitForEnd(jobs, running.Id);

        Assert.That(Current(jobs, queued.Id).StartedAtUtc, Is.Null, "the freed slot is not refilled on a disposed registry");
        await session.DidNotReceive().RescanAsync(1, Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>());
        Assert.That(jobs.Submit(Rescan(1)).Refusal?.Code, Is.EqualTo(JobErrorCodes.Busy));
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

    private string MakeBinData(string name, params (string File, int Size)[] files)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        foreach (var (file, size) in files)
            File.WriteAllBytes(Path.Combine(d, file), Enumerable.Repeat((byte)file[0], size).ToArray());
        return d;
    }

    private static JobSubmitRequest CreateWithHash(string root, string fileName, string filter = null,
        bool includeUniqueSizes = false) =>
        new()
        {
            Kind = JobKind.Create, RootPath = root, FileName = fileName,
            Hash = new HashOptions { Filter = filter, IncludeUniqueSizes = includeUniqueSizes }
        };

    private string[] HashedNames(int catalogId)
    {
        var children = _session.GetChildren(new EntryRefDto(catalogId, 0));
        using var lease = _session.AcquireRead(catalogId);
        return children.Where(c => lease.Source.IsHashDone(c.Ref.EntryIndex)).Select(c => c.Name).OrderBy(n => n).ToArray();
    }

    private bool IsFullHash(int catalogId, string name)
    {
        var entry = _session.GetChildren(new EntryRefDto(catalogId, 0)).Single(c => c.Name == name);
        using var lease = _session.AcquireRead(catalogId);
        return lease.Source.IsHashDone(entry.Ref.EntryIndex) && !lease.Source.IsPartialHash(entry.Ref.EntryIndex);
    }

    [Test]
    public async Task A_create_with_hash_scans_then_hashes_only_the_new_catalog_and_the_hashes_survive_a_reload()
    {
        var jobs = Registry();
        using var subscription = jobs.Subscribe();
        var root = MakeBinData("fresh-root", ("dup1.bin", 3000), ("dup2.bin", 3000), ("lonely.bin", 4100), ("a.bin", 1));

        var job = Accepted(jobs.Submit(CreateWithHash(root, "fresh")));
        var seen = await ReadUntilEnd(subscription, job.Id);
        var done = seen[^1];

        Assert.That(done.State, Is.EqualTo(JobState.Succeeded), () => done.Error?.Message);
        Assert.That(done.Note, Does.Not.StartWith("Hashing"));
        var id = done.CatalogId!.Value;
        Assert.That(done.Result.Hash.FilesHashed, Is.EqualTo(2));
        Assert.That(HashedNames(id), Is.EqualTo(new[] { "dup1.bin", "dup2.bin" }),
            "a.bin shares its size with a file of catalog 0, which a create does not compare against");
        Assert.That(IsFullHash(id, "dup1.bin") && IsFullHash(id, "dup2.bin"), Is.True);
        Assert.That(IsHashed(0) || IsHashed(1), Is.False, "other catalogs are untouched");

        var phases = seen.Select(j => j.Phase).Distinct().ToList();
        Assert.That(phases, Is.SupersetOf(new[] { "scanning", "writing", "saving", "swapping" }));
        Assert.That(phases.IndexOf("scanning"), Is.LessThan(phases.IndexOf("swapping")));
        Assert.That(seen.Where(j => j.Phase == "scanning").All(j => j.Hash == null), Is.True);
        Assert.That(done.Hash, Is.Not.Null);

        await _session.LoadAsync(_cat);
        var reloaded = _session.GetCatalogs().Single(c => c.SourcePath == done.Result.SavedPath).CatalogId;
        Assert.That(HashedNames(reloaded), Is.EqualTo(new[] { "dup1.bin", "dup2.bin" }));
    }

    [Test]
    public async Task A_create_with_hash_respects_the_filter_and_the_unique_size_option()
    {
        var jobs = Registry();
        var root = MakeBinData("fresh-root", ("big1.bin", 4096), ("big2.bin", 4096), ("small1.txt", 100),
            ("small2.txt", 100), ("lonely.bin", 9000));

        var filtered = await WaitForEnd(jobs, Accepted(jobs.Submit(CreateWithHash(root, "filtered", "size:>1kb"))).Id);
        Assert.That(HashedNames(filtered.CatalogId!.Value), Is.EqualTo(new[] { "big1.bin", "big2.bin" }));

        var withUnique = await WaitForEnd(jobs,
            Accepted(jobs.Submit(CreateWithHash(root, "unique", "size:>1kb", includeUniqueSizes: true))).Id);
        Assert.That(HashedNames(withUnique.CatalogId!.Value), Is.EqualTo(new[] { "big1.bin", "big2.bin", "lonely.bin" }));
    }

    [Test]
    public void An_invalid_filter_refuses_the_create_before_anything_is_scanned_or_written()
    {
        var jobs = Registry();
        var root = MakeBinData("fresh-root", ("a.bin", 3000), ("b.bin", 3000));
        var parserMessage = Assert.Throws<cdeLib.EverythingQueryException>(() => cdeLib.EverythingQuery.Parse("size:>>")).Message;

        var refusal = jobs.Submit(CreateWithHash(root, "fresh", "size:>>")).Refusal;

        Assert.That(refusal.Code, Is.EqualTo(JobErrorCodes.InvalidFilter));
        Assert.That(refusal.Message, Is.EqualTo(parserMessage));
        Assert.That(jobs.GetJobs(), Is.Empty);
        Assert.That(Directory.GetFiles(_cat), Has.Length.EqualTo(2), "only the two catalogs of SetUp");
    }

    [Test]
    public async Task A_cancel_in_the_hash_stage_keeps_the_scan_and_records_no_hashes()
    {
        var root = MakeBinData("fresh-root", ("a.bin", 3000), ("b.bin", 3000));
        var created = await _session.CreateCatalogAsync(new CreateCatalogRequest { RootPath = root, FileName = "fresh" });
        var before = File.ReadAllBytes(created.SavedPath);
        using var cts = new CancellationTokenSource();
        var progress = new HashSink(p =>
        {
            if (p.Phase == "saving") cts.Cancel();
        });

        Assert.That(async () => await _session.HashAsync([created.CatalogId], null, progress, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(File.ReadAllBytes(created.SavedPath), Is.EqualTo(before));
        Assert.That(Directory.GetFiles(_cat, "*.tmp"), Is.Empty);
        Assert.That(HashedNames(created.CatalogId), Is.Empty);
        Assert.That(_session.GetCatalogs().Any(c => c.CatalogId == created.CatalogId), Is.True);
    }

    private sealed class HashSink(Action<HashProgress> report) : IProgress<HashProgress>
    {
        public void Report(HashProgress value) => report(value);
    }

    private static ICatalogSession FakeCreateSession(Func<IReadOnlyList<int>, CancellationToken, Task<HashResult>> hash)
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightCreate(Arg.Any<CreatePreflightRequest>()).Returns(new CreatePreflightResult
        {
            FileName = "fresh", CatalogDir = "cat", TargetPath = Path.Combine(Path.GetTempPath(), "fresh.cdex"),
            RootAccessible = true, Writable = true
        });
        session.CreateCatalogAsync(Arg.Any<CreateCatalogRequest>(), Arg.Any<IProgress<CreateProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCatalogResult { CatalogId = 7, RootPath = "root", Entries = 3, SavedPath = "fresh.cdex" });
        session.HashAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<HashOptions>(), Arg.Any<IProgress<HashProgress>>(), Arg.Any<CancellationToken>())
            .Returns(ci => hash(ci.Arg<IReadOnlyList<int>>(), ci.Arg<CancellationToken>()));
        return session;
    }

    [Test]
    public async Task A_cancel_in_the_hash_stage_ends_the_create_as_succeeded_with_a_note()
    {
        var started = new TaskCompletionSource();
        IReadOnlyList<int> hashed = null;
        _jobs = new JobRegistry(FakeCreateSession(async (ids, ct) =>
        {
            hashed = ids;
            started.SetResult();
            await Task.Delay(Timeout, ct);
            return new HashResult();
        }));

        var job = Accepted(_jobs.Submit(CreateWithHash("root", "fresh")));
        await started.Task;
        _jobs.Cancel(job.Id);
        var done = await WaitForEnd(_jobs, job.Id);

        Assert.That(hashed, Is.EqualTo(new[] { 7 }), "only the new catalog");
        Assert.That(done.State, Is.EqualTo(JobState.Succeeded));
        Assert.That(done.CatalogId, Is.EqualTo(7));
        Assert.That(done.Note, Is.EqualTo("Hashing cancelled"));
        Assert.That(done.Result.Hash, Is.Null);
    }

    [Test]
    public async Task A_failure_in_the_hash_stage_ends_the_create_as_succeeded_with_the_message()
    {
        _jobs = new JobRegistry(FakeCreateSession((_, _) => throw new InvalidOperationException("disk gone")));

        var job = Accepted(_jobs.Submit(CreateWithHash("root", "fresh")));
        var done = await WaitForEnd(_jobs, job.Id);

        Assert.That(done.State, Is.EqualTo(JobState.Succeeded));
        Assert.That(done.CatalogId, Is.EqualTo(7));
        Assert.That(done.Note, Is.EqualTo("Hashing failed: disk gone"));
        Assert.That(done.Error, Is.Null);
    }

    [Test]
    public async Task A_hash_stage_failure_after_some_hashes_were_saved_reports_them()
    {
        _jobs = new JobRegistry(FakeCreateSession((_, _) =>
            throw new HashFailedException("Hash FAILED: 2 files hashed, 0 files could not be read; unexpected error: boom",
                new InvalidOperationException("boom"), filesSaved: 2, filesFailed: 0)));

        var job = Accepted(_jobs.Submit(CreateWithHash("root", "fresh")));
        var done = await WaitForEnd(_jobs, job.Id);

        Assert.That(done.State, Is.EqualTo(JobState.Succeeded));
        Assert.That(done.Note, Does.StartWith("Hashing failed: Hash FAILED"));
        Assert.That(done.Result.Hash, Is.Not.Null, "the saved hashes are reported, not dropped");
        Assert.That((done.Result.Hash.FilesHashed, done.Result.Hash.FilesFailed), Is.EqualTo((2L, 0L)));
    }

    [Test]
    public async Task A_create_without_hash_never_hashes()
    {
        var session = FakeCreateSession((_, _) => throw new InvalidOperationException("must not run"));
        _jobs = new JobRegistry(session);

        var done = await WaitForEnd(_jobs, Accepted(_jobs.Submit(Create("root", "fresh"))).Id);

        Assert.That(done.State, Is.EqualTo(JobState.Succeeded));
        Assert.That(done.Note, Is.Null);
        await session.DidNotReceiveWithAnyArgs().HashAsync(default, default, default, default);
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

    private static JobSubmitRequest Hash(int[] catalogIds, string filter = null) =>
        new() { Kind = JobKind.Hash, CatalogIds = catalogIds, Hash = new HashOptions { Filter = filter } };

    private bool IsHashed(int catalogId)
    {
        using var lease = _session.AcquireRead(catalogId);
        return lease.Source.IsHashDone(1);
    }

    [Test]
    public async Task A_hash_job_hashes_its_catalogs_as_one_set_and_reports_what_it_did()
    {
        var jobs = Registry();

        var job = Accepted(jobs.Submit(Hash([1, 0, 1])));
        var done = await WaitForEnd(jobs, job.Id);

        Assert.That(job.Kind, Is.EqualTo(JobKind.Hash));
        Assert.That(job.CatalogIds, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(job.CatalogId, Is.Null);
        Assert.That(job.CatalogName, Is.EqualTo("zero, one"));
        Assert.That(job.Title, Is.EqualTo("Hashing 2 catalogs"));
        Assert.That(done.State, Is.EqualTo(JobState.Succeeded), () => done.Error?.Message);
        Assert.That(done.CatalogId, Is.Null);
        Assert.That(done.Result.CatalogId, Is.Null);
        Assert.That((done.Result.Hash.FilesHashed, done.Result.Hash.FilesFailed), Is.EqualTo((2L, 0L)));
        Assert.That(done.Phase, Is.EqualTo("swapping"));
        Assert.That(done.Hash, Is.EqualTo(new HashJobProgress(2, 2, 2, 2)), "the two 1-byte files, read in the first pass");
        Assert.That(IsHashed(0) && IsHashed(1), Is.True, "a.txt of each catalog has the same size as the other");
    }

    [Test]
    public async Task A_running_hash_starts_in_the_hashing_phase()
    {
        var jobs = Registry();
        using var subscription = jobs.Subscribe();

        var job = Accepted(jobs.Submit(Hash([0])));
        var seen = await ReadUntilEnd(subscription, job.Id);

        Assert.That(seen.First(j => j.State == JobState.Running).Phase, Is.EqualTo("hashing"));
    }

    [Test]
    public async Task A_rescan_of_a_catalog_in_a_running_or_queued_hash_is_refused_and_so_is_the_reverse()
    {
        var jobs = Registry(maxConcurrent: 1);
        JobDto hash, rescan;
        using (new SwapBlockingLease(_session, 0))
        {
            hash = Accepted(jobs.Submit(Hash([0, 1])));
            await WaitAtSwap(jobs, hash.Id);

            Assert.That(jobs.Submit(Rescan(1)).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Assert.That(jobs.Submit(Rescan(0)).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Assert.That(jobs.Submit(Hash([1])).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
        }

        Assert.That((await WaitForEnd(jobs, hash.Id)).State, Is.EqualTo(JobState.Succeeded));

        using (new SwapBlockingLease(_session, 0))
        {
            rescan = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, rescan.Id);
            var queuedRescan = Accepted(jobs.Submit(Rescan(1)));

            Assert.That(jobs.Submit(Hash([0, 1])).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Assert.That(jobs.Submit(Hash([1])).Refusal.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning), "queued, not yet running");
            jobs.Cancel(queuedRescan.Id);
        }

        Assert.That((await WaitForEnd(jobs, rescan.Id)).State, Is.EqualTo(JobState.Succeeded));
        Assert.That(Accepted(jobs.Submit(Hash([0, 1]))).State, Is.AnyOf(JobState.Queued, JobState.Running));
    }

    [Test]
    public async Task A_hash_past_its_first_swap_finishes_when_cancelled()
    {
        var jobs = Registry();
        JobDto job;
        using (new SwapBlockingLease(_session, 0))
        {
            job = Accepted(jobs.Submit(Hash([0, 1])));
            await WaitAtSwap(jobs, job.Id);

            Assert.That(jobs.Cancel(job.Id), Is.True);
        }

        Assert.That((await WaitForEnd(jobs, job.Id)).State, Is.EqualTo(JobState.Succeeded));
        Assert.That(IsHashed(0) && IsHashed(1), Is.True);
    }

    [Test]
    public void A_hash_that_cannot_start_is_refused_with_its_code_and_lists_no_job()
    {
        var jobs = Registry();
        var parserMessage = Assert.Throws<cdeLib.EverythingQueryException>(() => cdeLib.EverythingQuery.Parse("size:>>")).Message;

        var badFilter = jobs.Submit(Hash([0], "size:>>")).Refusal;

        Assert.That(badFilter.Code, Is.EqualTo(JobErrorCodes.InvalidFilter));
        Assert.That(badFilter.Message, Is.EqualTo(parserMessage));
        Assert.That(jobs.Submit(Hash([])).Refusal.Code, Is.EqualTo(JobErrorCodes.InvalidRequest));
        Assert.That(jobs.Submit(new JobSubmitRequest { Kind = JobKind.Hash }).Refusal.Code, Is.EqualTo(JobErrorCodes.InvalidRequest));
        Assert.That(jobs.Submit(Hash([0, 5])).Refusal.Code, Is.EqualTo(JobErrorCodes.UnknownCatalog));
        Assert.That(jobs.GetJobs(), Is.Empty);
    }

    [Test]
    public async Task A_queued_hash_holds_off_a_reload_like_any_job()
    {
        var jobs = Registry(maxConcurrent: 1);
        JobDto rescan, hash;
        using (new SwapBlockingLease(_session, 0))
        {
            rescan = Accepted(jobs.Submit(Rescan(0)));
            await WaitAtSwap(jobs, rescan.Id);
            hash = Accepted(jobs.Submit(Hash([1])));

            Assert.That(jobs.TryBeginExclusive(), Is.Null);
        }

        await WaitForEnd(jobs, rescan.Id);
        Assert.That((await WaitForEnd(jobs, hash.Id)).State, Is.EqualTo(JobState.Succeeded));
    }

    private static JobState[] DistinctStates(IEnumerable<JobDto> jobs)
    {
        var states = new List<JobState>();
        foreach (var job in jobs)
            if (states.Count == 0 || states[^1] != job.State) states.Add(job.State);
        return states.ToArray();
    }

    /// <summary>Holds a read lease on one catalog (on its own thread, as the lock requires) until disposed.</summary>
    private sealed class SwapBlockingLease : IDisposable
    {
        private readonly ManualResetEventSlim _acquired = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Task _holder;

        public SwapBlockingLease(ICatalogSession session, int catalogId)
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
