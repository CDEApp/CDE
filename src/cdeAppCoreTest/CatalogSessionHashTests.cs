using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class CatalogSessionHashTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private string _dir;
    private string _cat;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-hash-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        Directory.CreateDirectory(_cat);
    }

    [TearDown]
    public void TearDown()
    {
        _session?.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { }
    }

    private string MakeData(string name, params (string File, int Size)[] files)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        foreach (var (file, size) in files) WriteFile(Path.Combine(d, file), size);
        return d;
    }

    private static void WriteFile(string path, int size)
    {
        File.WriteAllBytes(path, Enumerable.Repeat((byte)Path.GetFileName(path)[0], size).ToArray());
    }

    private const long GiB = 1L << 30;

    private string ScanToCdex(string dataDir, string cdexName, Dictionary<string, long> statedSizes = null)
    {
        var re = new RootEntry();
        re.PopulateRoot(dataDir);
        if (statedSizes != null)
        {
            foreach (var entry in re.Children) entry.Size = statedSizes[entry.Path];
        }

        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        var path = Path.Combine(_cat, cdexName);
        re.ActualFileName = path;
        ColumnarFormat.Write(re, path);
        return path;
    }

    private CatalogSession Load(params string[] cdexFiles)
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(cdexFiles.ToList());
        _session = new CatalogSession(loader, Substitute.For<Serilog.ILogger>());
        _session.LoadAsync("config").GetAwaiter().GetResult();
        return _session;
    }

    private static HashOptions Filter(string filter, bool includeUniqueSizes = false) =>
        new() { Filter = filter, IncludeUniqueSizes = includeUniqueSizes };

    private static string[] HashedNames(CatalogSession s, int catalogId)
    {
        var children = s.GetChildren(new EntryRefDto(catalogId, 0));
        using var lease = s.AcquireRead(catalogId);
        var source = lease.Source;
        return children.Where(c => source.IsHashDone(c.Ref.EntryIndex)).Select(c => c.Name).OrderBy(n => n).ToArray();
    }

    private string[] TempFiles() => Directory.GetFiles(_cat, "*.tmp");

    [Test]
    public async Task A_size_filter_hashes_the_matching_repeated_sizes_and_leaves_smaller_files_unhashed()
    {
        var data = MakeData("d", ("big1.bin", 4096), ("big2.bin", 4096), ("small1.txt", 100), ("small2.txt", 100),
            ("lonely.bin", 9000));
        var s = Load(ScanToCdex(data, "one.cdex", new Dictionary<string, long>
        {
            ["big1.bin"] = 3 * GiB, ["big2.bin"] = 3 * GiB, ["small1.txt"] = GiB, ["small2.txt"] = GiB,
            ["lonely.bin"] = 5 * GiB
        }));

        var result = await s.HashAsync([0], Filter("size:>2gb"));

        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "big1.bin", "big2.bin" }));
        Assert.That(result.FilesHashed, Is.EqualTo(2));
        Assert.That(result.FilesFailed, Is.EqualTo(0));
        Assert.That(TempFiles(), Is.Empty);
    }

    [Test]
    public async Task Including_unique_sizes_hashes_a_matching_file_no_other_file_shares_a_size_with()
    {
        var data = MakeData("d", ("big1.bin", 4096), ("big2.bin", 4096), ("small1.txt", 100), ("lonely.bin", 9000));
        var s = Load(ScanToCdex(data, "one.cdex"));

        await s.HashAsync([0], Filter("size:>1kb", includeUniqueSizes: true));

        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "big1.bin", "big2.bin", "lonely.bin" }));
    }

    [Test]
    public async Task Sizes_are_compared_across_every_catalog_of_the_run()
    {
        var first = ScanToCdex(MakeData("d0", ("a.bin", 3000), ("other.bin", 500)), "zero.cdex");
        var second = ScanToCdex(MakeData("d1", ("b.bin", 3000), ("another.bin", 700)), "one.cdex");
        var s = Load(first, second);

        await s.HashAsync([0], null);
        Assert.That(HashedNames(s, 0), Is.Empty, "alone, no file of catalog 0 shares its size");

        await s.HashAsync([1, 0], null);

        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "a.bin" }));
        Assert.That(HashedNames(s, 1), Is.EqualTo(new[] { "b.bin" }));
    }

    [Test]
    public async Task The_job_path_writes_the_same_bytes_as_cde_hash()
    {
        var files = new[]
        {
            ScanToCdex(MakeData("d0", ("copy1.bin", 5000), ("a.bin", 5000), ("x.txt", 80)), "zero.cdex"),
            ScanToCdex(MakeData("d1", ("copy2.bin", 5000), ("y.txt", 80), ("z.bin", 123)), "one.cdex")
        };
        var originals = files.Select(File.ReadAllBytes).ToArray();

        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(5000);
        config.HashFirstPassSize.Returns(new HashingSection().FirstPassSizeInBytes);
        var cli = new Duplication(Substitute.For<ILogger>(), config, Substitute.For<IApplicationDiagnostics>(),
            new OperationCancellation());
        await ColumnarCatalogHashing.HashAndSave(cli, files, null, selection: HashSelection.Parse("size:>1kb"));
        var fromCli = files.Select(File.ReadAllBytes).ToArray();
        for (var i = 0; i < files.Length; i++) File.WriteAllBytes(files[i], originals[i]);

        var s = Load(files);
        await s.HashAsync([0, 1], Filter("size:>1kb"));
        s.Dispose();

        for (var i = 0; i < files.Length; i++)
        {
            Assert.That(fromCli[i], Is.Not.EqualTo(originals[i]), "the run changed the catalog");
            Assert.That(File.ReadAllBytes(files[i]), Is.EqualTo(fromCli[i]), files[i]);
        }
    }

    [Test]
    public void A_cancel_before_the_first_swap_leaves_every_catalog_byte_identical_and_no_temp_file()
    {
        var files = new[]
        {
            ScanToCdex(MakeData("d0", ("a.bin", 3000)), "zero.cdex"),
            ScanToCdex(MakeData("d1", ("b.bin", 3000)), "one.cdex")
        };
        var originals = files.Select(File.ReadAllBytes).ToArray();
        var s = Load(files);
        using var cts = new CancellationTokenSource();
        var phases = new List<string>();
        var progress = new Sink(p =>
        {
            phases.Add(p.Phase);
            if (p.Phase == "saving") cts.Cancel();
        });

        Assert.That(async () => await s.HashAsync([0, 1], null, progress, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(phases, Does.Contain("saving").And.Not.Contain("swapping"));
        Assert.That(TempFiles(), Is.Empty);
        Assert.That(HashedNames(s, 0), Is.Empty);
        Assert.That(s.AnyRescanning, Is.False);
        s.Dispose();
        for (var i = 0; i < files.Length; i++)
            Assert.That(File.ReadAllBytes(files[i]), Is.EqualTo(originals[i]), files[i]);
    }

    [Test]
    public async Task An_invalid_filter_is_refused_with_the_search_bars_message_before_any_work()
    {
        var cdex = ScanToCdex(MakeData("d", ("a.bin", 3000), ("b.bin", 3000)), "one.cdex");
        var original = File.ReadAllBytes(cdex);
        var s = Load(cdex);
        var parserMessage = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("size:>>")).Message;

        var refusal = s.PreflightHash([0], Filter("size:>>"));
        var fromRun = await Refused(() => s.HashAsync([0], Filter("size:>>")));
        var fromPreview = Assert.Throws<HashRefusedException>(() => s.PreviewHash([0], Filter("size:>>")));

        Assert.That(refusal.Code, Is.EqualTo(JobErrorCodes.InvalidFilter));
        Assert.That(refusal.Message, Is.EqualTo(parserMessage));
        Assert.That(fromRun.Error.Code, Is.EqualTo(JobErrorCodes.InvalidFilter));
        Assert.That(fromPreview.Error.Message, Is.EqualTo(parserMessage));
        s.Dispose();
        Assert.That(File.ReadAllBytes(cdex), Is.EqualTo(original));
    }

    [Test]
    public async Task Hashes_survive_a_session_reload()
    {
        var s = Load(ScanToCdex(MakeData("d", ("a.bin", 3000), ("b.bin", 3000), ("c.txt", 5)), "one.cdex"));
        await s.HashAsync([0], null);

        await s.LoadAsync("config");

        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "a.bin", "b.bin" }));
    }

    [Test]
    public async Task A_duplicate_pair_promoted_to_full_hashes_counts_as_two_files()
    {
        var data = MakeData("d", ("a1.bin", 200_000), ("a2.bin", 200_000), ("b.bin", 200_000));
        var s = Load(ScanToCdex(data, "one.cdex"));

        var result = await s.HashAsync([0], null);

        Assert.That(result.FilesHashed, Is.EqualTo(3), "three files got a hash, though the pair was read twice");
    }

    [Test]
    public async Task After_a_rescan_a_second_run_reads_only_the_new_and_changed_files()
    {
        var data = MakeData("d", ("a.bin", 3000), ("b.bin", 3000), ("c.bin", 3000));
        var s = Load(ScanToCdex(data, "one.cdex"));
        Assert.That((await s.HashAsync([0], null)).FilesHashed, Is.EqualTo(3));

        WriteFile(Path.Combine(data, "new.bin"), 3000);
        File.WriteAllBytes(Path.Combine(data, "c.bin"), Enumerable.Repeat((byte)'z', 3000).ToArray());
        File.SetLastWriteTimeUtc(Path.Combine(data, "c.bin"), DateTime.UtcNow.AddMinutes(5));
        await s.RescanAsync(0);
        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "a.bin", "b.bin" }), "the rescan keeps hashes of unchanged files");

        var preview = s.PreviewHash([0], null);
        var second = await s.HashAsync([0], null);

        Assert.That((preview.Files, preview.AlreadyHashed), Is.EqualTo((2L, 2L)));
        Assert.That(second.FilesHashed, Is.EqualTo(2));
        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "a.bin", "b.bin", "c.bin", "new.bin" }));
    }

    [Test]
    public async Task The_preview_counts_what_the_run_then_reads_without_changing_anything()
    {
        var cdex = ScanToCdex(MakeData("d", ("big1.bin", 4096), ("big2.bin", 4096), ("s1.txt", 100), ("s2.txt", 100)),
            "one.cdex");
        var original = File.ReadAllBytes(cdex);
        var s = Load(cdex);

        var all = s.PreviewHash([0], null);
        var filtered = s.PreviewHash([0], Filter("size:>1kb"));

        Assert.That((all.Files, all.Bytes, all.AlreadyHashed), Is.EqualTo((4L, 8392L, 0L)));
        Assert.That((filtered.Files, filtered.Bytes, filtered.AlreadyHashed), Is.EqualTo((2L, 8192L, 0L)));
        Assert.That((await s.HashAsync([0], Filter("size:>1kb"))).FilesHashed, Is.EqualTo(filtered.Files));
        Assert.That(s.PreviewHash([0], Filter("size:>1kb")).AlreadyHashed, Is.EqualTo(2));
        Assert.That(original, Is.Not.EqualTo(File.ReadAllBytes(cdex)));
    }

    [Test]
    public async Task Progress_goes_through_the_phases_in_order_and_ends_each_pass_at_its_totals()
    {
        var s = Load(ScanToCdex(MakeData("d", ("copy1.bin", 100_000), ("copy2.bin", 100_000), ("c.txt", 5)), "one.cdex"));
        var reports = new List<HashProgress>();

        await s.HashAsync([0], null, new Sink(reports.Add));

        var phases = reports.Select(r => r.Phase).Distinct().ToArray();
        Assert.That(phases.Where(p => p is "saving" or "swapping"), Is.EqualTo(new[] { "saving", "swapping" }));
        Assert.That(phases.Except(["hashing", "hashing-full", "saving", "swapping"]), Is.Empty);
        var last = reports[^1];
        Assert.That(last.Counts, Is.EqualTo(new HashJobProgress(2, 2, 200_000, 200_000)));
        Assert.That(last.Note, Does.EndWith("one.cdex"));
    }

    [Test]
    public async Task A_search_holding_a_read_lease_keeps_its_data_while_the_swap_waits()
    {
        var s = Load(ScanToCdex(MakeData("d", ("a.bin", 3000), ("b.bin", 3000)), "one.cdex"));
        var atSwap = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Sink(p =>
        {
            if (p.Phase == "swapping") atSwap.TrySetResult();
        });
        var release = new ManualResetEventSlim();
        var acquired = new ManualResetEventSlim();
        string[] seenWhileWaiting = null;
        var reader = Task.Factory.StartNew(() =>
        {
            using var lease = s.AcquireRead(0);
            acquired.Set();
            release.Wait();
            seenWhileWaiting = Enumerable.Range(1, 2).Select(lease.Source.FullName).ToArray();
        }, TaskCreationOptions.LongRunning);
        acquired.Wait();

        var run = s.HashAsync([0], null, progress);
        await atSwap.Task.WaitAsync(Timeout);
        await Task.Delay(100);
        Assert.That(run.IsCompleted, Is.False, "the swap waits for the reader");
        release.Set();
        await reader;
        await run.WaitAsync(Timeout);

        Assert.That(seenWhileWaiting, Is.EquivalentTo(new[] { "a.bin", "b.bin" }));
        Assert.That(HashedNames(s, 0), Is.EqualTo(new[] { "a.bin", "b.bin" }));
    }

    [Test]
    public async Task A_rescan_and_a_second_hash_of_a_member_catalog_are_refused_while_a_hash_runs()
    {
        var s = Load(ScanToCdex(MakeData("d0", ("a.bin", 3000)), "zero.cdex"),
            ScanToCdex(MakeData("d1", ("b.bin", 3000)), "one.cdex"));
        var atSaving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new ManualResetEventSlim();
        var progress = new Sink(p =>
        {
            if (p.Phase != "saving") return;
            atSaving.TrySetResult();
            resume.Wait();
        });

        var run = s.HashAsync([0, 1], null, progress);
        await atSaving.Task.WaitAsync(Timeout);
        try
        {
            Assert.That(s.PreflightRescan(1), Is.EqualTo(RescanPreflight.AlreadyRunning));
            Assert.That(() => s.RescanAsync(1), Throws.InstanceOf<RescanAlreadyRunningException>());
            Assert.That(s.PreflightHash([1], null).Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            var second = await Refused(() => s.HashAsync([1], null));
            Assert.That(second.Error.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
            Assert.That(() => s.LoadAsync("config"), Throws.InvalidOperationException);
            Assert.That(s.PreviewHash([0, 1], null).Files, Is.EqualTo(2), "a preview only reads, so it is not refused");
        }
        finally
        {
            resume.Set();
        }

        await run.WaitAsync(Timeout);
        Assert.That(s.AnyRescanning, Is.False);
        Assert.That(s.PreflightRescan(1), Is.EqualTo(RescanPreflight.Ready));
    }

    [Test]
    public async Task A_hash_is_refused_while_a_member_catalog_is_being_rescanned_and_takes_no_catalog()
    {
        var data = MakeData("d1", ("b.bin", 3000));
        var s = Load(ScanToCdex(MakeData("d0", ("a.bin", 3000)), "zero.cdex"), ScanToCdex(data, "one.cdex"));
        var release = new ManualResetEventSlim();
        var acquired = new ManualResetEventSlim();
        var reader = Task.Factory.StartNew(() =>
        {
            using var lease = s.AcquireRead(1);
            acquired.Set();
            release.Wait();
        }, TaskCreationOptions.LongRunning);
        acquired.Wait();
        var atSwap = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rescan = s.RescanAsync(1, new RescanSink(p =>
        {
            if (p.Phase == "swapping") atSwap.TrySetResult();
        }));
        await atSwap.Task.WaitAsync(Timeout);

        var refused = await Refused(() => s.HashAsync([0, 1], null));

        Assert.That(refused.Error.Code, Is.EqualTo(JobErrorCodes.AlreadyRunning));
        Assert.That(s.IsRescanning(0), Is.False, "the catalog taken before the refusal is released");
        release.Set();
        await reader;
        await rescan.WaitAsync(Timeout);
    }

    [Test]
    public async Task Unknown_empty_and_unreachable_catalogs_are_refused()
    {
        var data = MakeData("d", ("a.bin", 3000), ("b.bin", 3000));
        var s = Load(ScanToCdex(data, "one.cdex"));

        Assert.That(s.PreflightHash([], null).Code, Is.EqualTo(JobErrorCodes.InvalidRequest));
        Assert.That(s.PreflightHash(null, null).Code, Is.EqualTo(JobErrorCodes.InvalidRequest));
        Assert.That(s.PreflightHash([0, 7], null).Code, Is.EqualTo(JobErrorCodes.UnknownCatalog));
        Assert.That(s.PreflightHash([0, 0], null), Is.Null);

        Directory.Delete(data, true);
        var refusal = s.PreflightHash([0], null);
        Assert.That(refusal.Code, Is.EqualTo(JobErrorCodes.RootNotAccessible));
        Assert.That(refusal.RootPath, Is.EqualTo(data).IgnoreCase);
        var fromRun = await Refused(() => s.HashAsync([0], null));
        Assert.That(fromRun.Error.Code, Is.EqualTo(JobErrorCodes.RootNotAccessible));
        Assert.That(s.PreviewHash([0], null).Files, Is.EqualTo(2), "a preview does not need the drive");
    }

    [Test]
    public async Task A_legacy_cde_catalog_is_refused()
    {
        var data = MakeData("d", ("a.bin", 3000), ("b.bin", 3000));
        var cde = Path.Combine(_cat, "legacy.cde");
        File.WriteAllBytes(cde, [1, 2, 3]);
        var tree = new RootEntry();
        tree.PopulateRoot(data);
        tree.SortAllChildrenByPath();
        tree.SetSummaryFields();
        tree.ActualFileName = cde;
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(new List<string>());
        loader.LoadRootEntriesAsync(Arg.Any<string>(), Arg.Any<Action<int, int, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<RootEntry> { tree }));
        _session = new CatalogSession(loader, Substitute.For<Serilog.ILogger>());
        await _session.LoadAsync("config");

        Assert.That(_session.PreflightHash([0], null).Code, Is.EqualTo(JobErrorCodes.LegacyCatalog));
        var fromRun = await Refused(() => _session.HashAsync([0], null));
        Assert.That(fromRun.Error.Code, Is.EqualTo(JobErrorCodes.LegacyCatalog));
        var fromPreview = Assert.Throws<HashRefusedException>(() => _session.PreviewHash([0], null));
        Assert.That(fromPreview.Error.Code, Is.EqualTo(JobErrorCodes.LegacyCatalog));
        Assert.That(File.ReadAllBytes(cde), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task The_first_pass_size_comes_from_the_hashing_config_current_at_the_run()
    {
        var cdex = ScanToCdex(MakeData("d", ("a.bin", 3000), ("b.bin", 3000)), "one.cdex");
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns([cdex]);
        _session = new CatalogSession(loader, Substitute.For<Serilog.ILogger>(),
            hashingConfig: () => new HashingSection { FirstPassSizeInBytes = 1000 });
        await _session.LoadAsync("config");

        await _session.HashAsync([0], null);

        using var lease = _session.AcquireRead(0);
        Assert.That(lease.Source.IsPartialHash(1), Is.True, "a 3000-byte file read to 1000 bytes has a partial hash");
    }

    private static async Task<HashRefusedException> Refused(Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (HashRefusedException ex)
        {
            return ex;
        }

        Assert.Fail("the run was not refused");
        return null;
    }

    private sealed class Sink(Action<HashProgress> onReport) : IProgress<HashProgress>
    {
        public void Report(HashProgress value) => onReport(value);
    }

    private sealed class RescanSink(Action<RescanProgress> onReport) : IProgress<RescanProgress>
    {
        public void Report(RescanProgress value) => onReport(value);
    }
}
