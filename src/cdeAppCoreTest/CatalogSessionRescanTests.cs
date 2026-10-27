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

    private string ScanToCdex(string dataDir, string cdexName, string description = null, Action<RootEntry> tweak = null)
    {
        var re = new RootEntry();
        re.PopulateRoot(dataDir);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        re.Description = description;
        tweak?.Invoke(re);
        var path = Path.Combine(_cat, cdexName);
        re.ActualFileName = path;
        ColumnarFormat.Write(re, path);
        return path;
    }

    private CatalogSession LoadCdex(params string[] cdexFiles)
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(cdexFiles.ToList());
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
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

    private sealed class SyncProgress(Action<RescanProgress> onReport) : IProgress<RescanProgress>
    {
        public void Report(RescanProgress value) => onReport(value);
    }
}
