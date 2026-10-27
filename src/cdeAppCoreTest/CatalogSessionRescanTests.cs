using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

/// <summary>
/// Rescan = scan the catalog's root, write beside the source file, swap under a per-catalog write lock.
/// Uses real temp folders: a "data" folder that gets scanned and a "cat" folder holding the catalogs.
/// </summary>
[TestFixture]
public class CatalogSessionRescanTests
{
    private string _dir;
    private string _cat;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-rescan-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        Directory.CreateDirectory(_cat);
    }

    [TearDown]
    public void TearDown()
    {
        _session?.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private string MakeData(string name, params string[] files)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        foreach (var f in files) File.WriteAllText(Path.Combine(d, f), "x" + f);
        return d;
    }

    private string ScanToCdex(string dataDir, string cdexName, string description = null, Action<RootEntry> tweak = null,
        bool followJunctions = false)
    {
        var re = new RootEntry();
        re.PopulateRoot(dataDir, followJunctions);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        re.Description = description;
        tweak?.Invoke(re);
        var path = Path.Combine(_cat, cdexName);
        re.ActualFileName = path;
        ColumnarFormat.Write(re, path);
        return path;
    }

    private CatalogSession LoadCdex(params string[] cdexFiles) => LoadCdexWith(null, cdexFiles);

    private CatalogSession LoadCdexWith(Func<string, ColumnarCatalogReader> openReader, params string[] cdexFiles)
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(cdexFiles.ToList());
        _session = new CatalogSession(loader, Substitute.For<ILogger>(), openReader);
        _session.LoadAsync("config").GetAwaiter().GetResult();
        return _session;
    }

    private static string[] RootNames(CatalogSession s, int catalogId) =>
        s.GetChildren(new EntryRefDto(catalogId, 0)).Select(c => c.Name).OrderBy(n => n).ToArray();

    [Test]
    public async Task Rescan_picks_up_new_files_and_keeps_the_catalog_id()
    {
        var data = MakeData("d1", "a.txt");
        var s = LoadCdex(ScanToCdex(data, "one.cdex"));
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");

        var result = await s.RescanAsync(0);

        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt", "b.txt" }));
        Assert.That(s.CatalogCount, Is.EqualTo(1));
        Assert.That(s.GetCatalogs()[0].CatalogId, Is.EqualTo(0));
        Assert.That(result.CatalogId, Is.EqualTo(0));
        Assert.That(result.Entries, Is.EqualTo(2)); // files + dirs, root excluded (as TotalEntryCount)
    }

    [Test]
    public void GetCatalogs_exposes_the_exact_source_path()
    {
        var path = ScanToCdex(MakeData("d1", "a.txt"), "one.cdex");
        var s = LoadCdex(path);
        Assert.That(s.GetCatalogs()[0].SourcePath, Is.EqualTo(Path.GetFullPath(path)).IgnoreCase);
    }

    [Test]
    public async Task Rescan_saves_over_the_source_file_not_the_default_name()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "custom-name.cdex");
        var s = LoadCdex(path);
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");
        var before = File.GetLastWriteTimeUtc(path);
        await Task.Delay(20);

        await s.RescanAsync(0);

        Assert.That(File.GetLastWriteTimeUtc(path), Is.GreaterThan(before));
        Assert.That(Directory.GetFiles(_cat), Is.EqualTo(new[] { path }).IgnoreCase, string.Join(",", Directory.GetFiles(_cat)));
        Assert.That(s.GetCatalogs()[0].SourcePath, Is.EqualTo(Path.GetFullPath(path)).IgnoreCase);
    }

    [Test]
    public async Task Rescan_leaves_other_catalogs_untouched()
    {
        var d1 = MakeData("d1", "a.txt");
        var d2 = MakeData("d2", "z.txt");
        var p1 = ScanToCdex(d1, "one.cdex");
        var p2 = ScanToCdex(d2, "two.cdex");
        var s = LoadCdex(p1, p2);
        var p2Stamp = File.GetLastWriteTimeUtc(p2);
        File.WriteAllText(Path.Combine(d1, "b.txt"), "new");
        File.WriteAllText(Path.Combine(d2, "y.txt"), "ignored until rescanned");

        await s.RescanAsync(0);

        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt", "b.txt" }));
        Assert.That(RootNames(s, 1), Is.EqualTo(new[] { "z.txt" }));
        Assert.That(File.GetLastWriteTimeUtc(p2), Is.EqualTo(p2Stamp));
    }

    [Test]
    public async Task Rescan_carries_the_description_forward()
    {
        var data = MakeData("d1", "a.txt");
        var s = LoadCdex(ScanToCdex(data, "one.cdex", description: "my backup disk"));

        await s.RescanAsync(0);

        Assert.That(s.GetCatalogs()[0].Description, Is.EqualTo("my backup disk"));
    }

    [Test]
    public async Task Rescan_keeps_hashes_of_unchanged_files_only()
    {
        var data = MakeData("d1", "same.txt", "changed.txt");
        var hash = new Hash16();
        var s = LoadCdex(ScanToCdex(data, "one.cdex", tweak: re =>
        {
            foreach (var child in re.Children)
            {
                child.Hash = new Hash16(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
                child.IsHashDone = true;
                hash = child.Hash;
            }
        }));
        File.WriteAllText(Path.Combine(data, "changed.txt"), "different length now");

        await s.RescanAsync(0);

        var kids = s.GetChildren(new EntryRefDto(0, 0)).ToDictionary(c => c.Name);
        using var lease = s.AcquireRead(0);
        Assert.That(lease.Source.IsHashDone(kids["same.txt"].Ref.EntryIndex), Is.True);
        Assert.That(lease.Source.HashOf(kids["same.txt"].Ref.EntryIndex), Is.EqualTo(hash));
        Assert.That(lease.Source.IsHashDone(kids["changed.txt"].Ref.EntryIndex), Is.False);
    }

    [Test]
    public async Task Rescan_of_an_unreachable_root_throws_and_changes_nothing()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "one.cdex");
        var s = LoadCdex(path);
        var bytes = File.ReadAllBytes(path);
        Directory.Delete(data, true);

        Assert.That(s.PreflightRescan(0), Is.EqualTo(RescanPreflight.RootNotAccessible));
        await Assert.ThrowsAsync<RootNotAccessibleException>(() => s.RescanAsync(0));

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(File.Exists(path + ".tmp"), Is.False);
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt" }));
        Assert.That(s.IsRescanning(0), Is.False);
    }

    [Test]
    public void Preflight_reports_unknown_and_ready()
    {
        var s = LoadCdex(ScanToCdex(MakeData("d1", "a.txt"), "one.cdex"));
        Assert.That(s.PreflightRescan(0), Is.EqualTo(RescanPreflight.Ready));
        Assert.That(s.PreflightRescan(7), Is.EqualTo(RescanPreflight.UnknownCatalog));
        Assert.That(s.PreflightRescan(-1), Is.EqualTo(RescanPreflight.UnknownCatalog));
    }

    [Test]
    public async Task Cancelled_rescan_leaves_old_file_and_data_and_no_tmp()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "one.cdex");
        var s = LoadCdex(path);
        var bytes = File.ReadAllBytes(path);
        for (var i = 0; i < 1500; i++) File.WriteAllText(Path.Combine(data, $"f{i}.txt"), "");
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(p => { if (p.Phase == "scanning") cts.Cancel(); });

        await Assert.ThrowsAsync<OperationCanceledException>(() => s.RescanAsync(0, progress, cts.Token));

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Directory.GetFiles(_cat), Is.EqualTo(new[] { path }).IgnoreCase, string.Join(",", Directory.GetFiles(_cat)));
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt" }));
        Assert.That(s.IsRescanning(0), Is.False);
    }

    [Test]
    [Platform("Win")]
    public async Task Failure_during_the_swap_restores_the_old_catalog_and_removes_the_tmp()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "one.cdex");
        var s = LoadCdex(path);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");

        // A foreign open handle without delete-sharing makes the rename-over fail once our mapping is released.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.CatchAsync<Exception>(() => s.RescanAsync(0)); // sharing violation (IOException or UnauthorizedAccess)
        }

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Directory.GetFiles(_cat), Is.EqualTo(new[] { path }).IgnoreCase, string.Join(",", Directory.GetFiles(_cat)));
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt" }), "old data still served from a re-opened mapping");
    }

    [Test]
    public async Task A_reader_holding_the_lock_finishes_while_the_swap_waits()
    {
        var data = MakeData("d1", "a.txt");
        var s = LoadCdex(ScanToCdex(data, "one.cdex"));
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");

        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        string[] namesSeenByReader = null;
        var reader = Task.Run(() =>
        {
            using var lease = s.AcquireRead(0);
            acquired.Set();
            release.Wait();
            // The old mapping must still be valid here even though the rescan is ready to swap.
            namesSeenByReader = Enumerable.Range(1, lease.Source.Count - 1).Select(lease.Source.FullName).OrderBy(n => n).ToArray();
        });
        acquired.Wait();

        var swapping = new TaskCompletionSource();
        var rescan = s.RescanAsync(0, new SyncProgress(p => { if (p.Phase == "swapping") swapping.TrySetResult(); }));
        await swapping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        Assert.That(rescan.IsCompleted, Is.False, "swap must wait for the running reader");

        release.Set();
        await reader;
        await rescan.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(namesSeenByReader, Is.EqualTo(new[] { "a.txt" }));
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt", "b.txt" }));
    }

    [Test]
    public async Task Only_one_rescan_per_catalog_at_a_time()
    {
        var data = MakeData("d1", "a.txt");
        var s = LoadCdex(ScanToCdex(data, "one.cdex"));
        using var hold = new ManualResetEventSlim();
        using var acquired = new ManualResetEventSlim();
        var blocker = Task.Run(() =>
        {
            using var lease = s.AcquireRead(0);
            acquired.Set();
            hold.Wait();
        });
        acquired.Wait();
        var swapping = new TaskCompletionSource();
        var first = s.RescanAsync(0, new SyncProgress(p => { if (p.Phase == "swapping") swapping.TrySetResult(); }));
        await swapping.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(s.IsRescanning(0), Is.True);
        Assert.That(s.AnyRescanning, Is.True);
        Assert.That(s.PreflightRescan(0), Is.EqualTo(RescanPreflight.AlreadyRunning));
        await Assert.ThrowsAsync<RescanAlreadyRunningException>(() => s.RescanAsync(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.LoadAsync("config"), "reload refused while rescanning");

        hold.Set();
        await blocker;
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(s.AnyRescanning, Is.False);
    }

    [Test]
    public async Task Legacy_cde_catalog_is_saved_as_cdex_and_the_old_file_becomes_bak()
    {
        var data = MakeData("d1", "a.txt");
        var cde = Path.Combine(_cat, "legacy.cde");
        File.WriteAllBytes(cde, [1, 2, 3]);
        var tree = new RootEntry();
        tree.PopulateRoot(data);
        tree.SortAllChildrenByPath();
        tree.SetSummaryFields();
        tree.Description = "legacy desc";
        tree.ActualFileName = cde;
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(new List<string>());
        loader.LoadRootEntriesAsync(Arg.Any<string>(), Arg.Any<Action<int, int, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<RootEntry> { tree }));
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await _session.LoadAsync("config");
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");
        Assert.That(_session.GetCatalogs()[0].SourcePath, Is.EqualTo(Path.GetFullPath(cde)).IgnoreCase);

        var result = await _session.RescanAsync(0);

        var cdex = Path.ChangeExtension(cde, ".cdex");
        Assert.That(File.Exists(cdex), Is.True);
        Assert.That(File.Exists(cde), Is.False);
        Assert.That(File.Exists(cde + ".bak"), Is.True);
        Assert.That(Directory.GetFiles(_cat).Select(Path.GetFileName), Is.EquivalentTo(new[] { "legacy.cdex", "legacy.cde.bak" }));
        Assert.That(result.SavedPath, Is.EqualTo(cdex).IgnoreCase);
        Assert.That(_session.GetCatalogs()[0].SourcePath, Is.EqualTo(cdex).IgnoreCase);
        Assert.That(_session.GetCatalogs()[0].Description, Is.EqualTo("legacy desc"));
        Assert.That(RootNames(_session, 0), Is.EqualTo(new[] { "a.txt", "b.txt" }));
    }

    [Test]
    public async Task Rescan_keeps_follow_junctions_true_from_the_stored_catalog()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "fj.cdex", followJunctions: true);
        var s = LoadCdex(path);
        Assert.That(s.GetCatalogs()[0].FollowJunctions, Is.True);

        await s.RescanAsync(0);

        Assert.That(s.GetCatalogs()[0].FollowJunctions, Is.True);
        s.Dispose();
        using var reader = new ColumnarCatalogReader(path);
        Assert.That(reader.FollowJunctions, Is.True, "the rewritten file keeps the header bit");
    }

    [Test]
    public async Task Rescan_keeps_follow_junctions_false()
    {
        var path = ScanToCdex(MakeData("d1", "a.txt"), "nofj.cdex");
        var s = LoadCdex(path);

        await s.RescanAsync(0);

        Assert.That(s.GetCatalogs()[0].FollowJunctions, Is.False);
    }

    // Opens normally until armed, then fails the next `failures` opens (-1 = every open).
    private sealed class FlakyOpener
    {
        public int Failures;

        public ColumnarCatalogReader Open(string path)
        {
            if (Failures != 0)
            {
                if (Failures > 0) Failures--;
                throw new InvalidDataException("injected open failure");
            }

            return new ColumnarCatalogReader(path);
        }
    }

    [Test]
    public async Task Failure_opening_the_new_file_restores_the_previous_file_and_catalog()
    {
        var data = MakeData("d1", "a.txt");
        var path = ScanToCdex(data, "one.cdex");
        var opener = new FlakyOpener();
        var s = LoadCdexWith(opener.Open, path);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllText(Path.Combine(data, "b.txt"), "new");
        opener.Failures = 1; // the new file fails to open; re-opening the old one works

        await Assert.ThatAsync(() => s.RescanAsync(0), Throws.TypeOf<InvalidDataException>());

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "old file restored byte for byte");
        Assert.That(Directory.GetFiles(_cat), Is.EqualTo(new[] { path }).IgnoreCase, "no .tmp / .old left behind");
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt" }));
    }

    [Test]
    public async Task Roots_stay_usable_after_a_failed_swap()
    {
        var data = MakeData("d1", "a.txt");
        var opener = new FlakyOpener();
        var s = LoadCdexWith(opener.Open, ScanToCdex(data, "one.cdex"));
        opener.Failures = 1;

        await Assert.ThatAsync(() => s.RescanAsync(0), Throws.TypeOf<InvalidDataException>());

        Assert.That(s.Roots[0].Path, Is.EqualTo(s.GetCatalogs()[0].RootPath));
    }

    [Test]
    public async Task An_unavailable_catalog_does_not_break_the_others()
    {
        var d1 = MakeData("d1", "a.txt");
        var d2 = MakeData("d2", "z.txt");
        var opener = new FlakyOpener();
        var s = LoadCdexWith(opener.Open, ScanToCdex(d1, "one.cdex"), ScanToCdex(d2, "two.cdex"));
        opener.Failures = -1; // neither the new file nor the old one can be re-opened

        await Assert.ThatAsync(() => s.RescanAsync(0), Throws.TypeOf<InvalidDataException>());
        opener.Failures = 0;

        var catalogs = s.GetCatalogs();
        Assert.That(catalogs.Select(c => c.CatalogId), Is.EqualTo(new[] { 1 }));
        Assert.That(s.TotalEntryCount, Is.EqualTo(1));
        Assert.That(s.CatalogCount, Is.EqualTo(2));
        Assert.That(RootNames(s, 1), Is.EqualTo(new[] { "z.txt" }));
        Assert.That(s.PreflightRescan(0), Is.EqualTo(RescanPreflight.Unavailable));
    }

    private sealed class BlockingLoader : ILoadCatalogService
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        public bool Block;
        public string Cdex;

        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath)
        {
            if (Block)
            {
                Entered.Set();
                Release.Wait();
            }

            return [Cdex];
        }
    }

    [Test]
    public async Task Rescan_is_refused_while_a_load_runs_and_a_second_load_too()
    {
        var path = ScanToCdex(MakeData("d1", "a.txt"), "one.cdex");
        var loader = new BlockingLoader { Cdex = path };
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await _session.LoadAsync("c");
        loader.Block = true;

        var load = Task.Run(() => _session.LoadAsync("c"));
        loader.Entered.Wait();
        try
        {
            Assert.That(_session.PreflightRescan(0), Is.EqualTo(RescanPreflight.Busy));
            await Assert.ThatAsync(() => _session.RescanAsync(0), Throws.TypeOf<InvalidOperationException>());
            Assert.That(_session.AnyRescanning, Is.False, "a refused rescan must not leave the flag set");
            await Assert.ThatAsync(() => _session.LoadAsync("c"), Throws.TypeOf<InvalidOperationException>());
        }
        finally
        {
            loader.Release.Set();
        }

        await load;
        Assert.That(_session.PreflightRescan(0), Is.EqualTo(RescanPreflight.Ready));
    }

    [Test]
    public async Task A_catalog_without_a_source_file_is_refused_up_front()
    {
        var tree = new RootEntry();
        tree.PopulateRoot(MakeData("d1", "a.txt"));
        tree.SetSummaryFields();
        tree.ActualFileName = null;
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(new List<string>());
        loader.LoadRootEntriesAsync(Arg.Any<string>(), Arg.Any<Action<int, int, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<RootEntry> { tree }));
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await _session.LoadAsync("c");

        Assert.That(_session.PreflightRescan(0), Is.EqualTo(RescanPreflight.NoSourceFile));
        await Assert.ThatAsync(() => _session.RescanAsync(0), Throws.TypeOf<NoSourceFileException>());
    }

    private sealed class SyncProgress(Action<RescanProgress> onReport) : IProgress<RescanProgress>
    {
        public void Report(RescanProgress value) => onReport(value);
    }
}
