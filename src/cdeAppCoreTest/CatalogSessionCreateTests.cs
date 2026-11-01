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
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

/// <summary>
/// Create = scan a root, write <c>&lt;working folder&gt;\&lt;name&gt;.cdex</c> via a .tmp and a no-overwrite rename, and
/// append the catalog to the session under the session gate. Real temp folders: "data*" are scanned, "cat" is the
/// working folder.
/// </summary>
[TestFixture]
public class CatalogSessionCreateTests
{
    private string _dir;
    private string _cat;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-create-" + Guid.NewGuid().ToString("N"));
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

    // Loads from _cat so the session's working folder is the temp "cat" folder.
    private CatalogSession Load(ILoadCatalogService loader, Func<string, ColumnarCatalogReader> openReader = null)
    {
        _session = new CatalogSession(loader, Substitute.For<ILogger>(), openReader);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
        return _session;
    }

    private CatalogSession LoadFiles(params string[] cdexFiles)
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_ => Directory.GetFiles(_cat, "*.cdex").ToList());
        return Load(loader);
    }

    private static string[] RootNames(CatalogSession s, int catalogId) =>
        s.GetChildren(new EntryRefDto(catalogId, 0)).Select(c => c.Name).OrderBy(n => n).ToArray();

    private static CreateCatalogRequest Req(string root, string name = "new", string description = null,
        bool followJunctions = false) =>
        new() { RootPath = root, FileName = name, Description = description, FollowJunctions = followJunctions };

    private sealed class Capture : IProgress<CreateProgress>
    {
        public readonly List<CreateProgress> Reports = [];
        public Action<CreateProgress> OnReport;

        public void Report(CreateProgress value)
        {
            lock (Reports) Reports.Add(value);
            OnReport?.Invoke(value);
        }
    }

    private CatalogSession LoadWithScanConfig(Func<ScanSection> scanConfig)
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_ => Directory.GetFiles(_cat, "*.cdex").ToList());
        _session = new CatalogSession(loader, Substitute.For<ILogger>(), scanConfig: scanConfig);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
        return _session;
    }

    [Test]
    public async Task Create_skips_configured_exclude_paths_but_records_the_folder_as_an_empty_entry()
    {
        var data = MakeData("d1", "keep.txt");
        var skip = Path.Combine(data, "skip");
        Directory.CreateDirectory(skip);
        File.WriteAllText(Path.Combine(skip, "inner.txt"), "x");
        var s = LoadWithScanConfig(() => new ScanSection { ExcludePaths = [skip], UseDefaultExcludes = false });

        var result = await s.CreateCatalogAsync(Req(data));

        Assert.That(RootNames(s, result.CatalogId), Is.EqualTo(new[] { "keep.txt", "skip" }));
        Assert.That(result.Entries, Is.EqualTo(2)); // skip dir + keep.txt, inner.txt not scanned
    }

    [Test]
    public async Task Create_reports_the_excluded_and_access_denied_folder_counts()
    {
        var data = MakeData("d1", "keep.txt");
        var skip = Path.Combine(data, "skip");
        Directory.CreateDirectory(skip);
        var s = LoadWithScanConfig(() => new ScanSection { ExcludePaths = [skip], UseDefaultExcludes = false });

        var result = await s.CreateCatalogAsync(Req(data));

        Assert.That(result.ExcludedFolderCount, Is.EqualTo(1));
        Assert.That(result.AccessDeniedFolderCount, Is.Zero);
    }

    [Test]
    public async Task Create_with_nothing_skipped_reports_zero_counts()
    {
        var s = LoadFiles();

        var result = await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt")));

        Assert.That(result.ExcludedFolderCount, Is.Zero);
        Assert.That(result.AccessDeniedFolderCount, Is.Zero);
    }

    [Test]
    public async Task Create_without_a_scan_config_provider_excludes_nothing()
    {
        var data = MakeData("d1", "keep.txt");
        Directory.CreateDirectory(Path.Combine(data, "skip"));
        File.WriteAllText(Path.Combine(data, "skip", "inner.txt"), "x");
        var s = LoadFiles();

        var result = await s.CreateCatalogAsync(Req(data));

        Assert.That(result.Entries, Is.EqualTo(3));
    }

    [Test]
    public async Task Create_adds_a_new_catalog_with_a_new_id_and_leaves_existing_ones_untouched()
    {
        var d1 = MakeData("d1", "a.txt");
        var p1 = ScanToCdex(d1, "one.cdex");
        var s = LoadFiles();
        var before = s.GetCatalogs().Single();
        var oneBytes = File.ReadAllBytes(p1);

        var result = await s.CreateCatalogAsync(Req(MakeData("d2", "z.txt", "y.txt")));

        Assert.That(result.CatalogId, Is.EqualTo(1));
        Assert.That(s.CatalogCount, Is.EqualTo(2));
        Assert.That(s.GetCatalogs().Select(c => c.CatalogId), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(s.GetCatalogs()[0].RootPath, Is.EqualTo(before.RootPath));
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt" }));
        Assert.That(RootNames(s, 1), Is.EqualTo(new[] { "y.txt", "z.txt" }));
        Assert.That(result.Entries, Is.EqualTo(2));
        Assert.That(s.Roots.Count, Is.EqualTo(2));
        Assert.That(File.ReadAllBytes(p1), Is.EqualTo(oneBytes));
    }

    [Test]
    public async Task Create_writes_the_file_by_full_path_into_the_working_folder_not_the_current_directory()
    {
        var s = LoadFiles();
        var cwdBefore = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cdex").Length;

        var result = await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "my-name"));

        var expected = Path.Combine(Path.GetFullPath(_cat), "my-name.cdex");
        Assert.That(result.SavedPath, Is.EqualTo(expected).IgnoreCase);
        Assert.That(File.Exists(expected), Is.True);
        Assert.That(Directory.GetFiles(_cat), Is.EqualTo(new[] { expected }).IgnoreCase);
        Assert.That(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cdex").Length, Is.EqualTo(cwdBefore));
        Assert.That(s.GetCatalogs().Single().SourcePath, Is.EqualTo(expected).IgnoreCase);
    }

    [Test]
    public async Task Create_uses_the_default_name_when_none_is_given()
    {
        var s = LoadFiles();
        var data = MakeData("d1", "a.txt");

        var result = await s.CreateCatalogAsync(Req(data, null));

        Assert.That(Path.GetFileName(result.SavedPath),
            Is.EqualTo(CatalogNaming.DefaultFileName(data) + ".cdex"));
    }

    [Test]
    public async Task Create_appends_cdex_once_even_if_the_user_typed_it()
    {
        var s = LoadFiles();
        var result = await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "typed.cdex"));
        Assert.That(Path.GetFileName(result.SavedPath), Is.EqualTo("typed.cdex"));
    }

    [Test]
    public async Task Create_stores_the_description_and_follow_junctions_in_the_file()
    {
        var s = LoadFiles();
        var result = await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "j", "holiday photos", followJunctions: true));

        var info = s.GetCatalogs().Single();
        Assert.That(info.Description, Is.EqualTo("holiday photos"));
        Assert.That(info.FollowJunctions, Is.True);
        using var reader = new ColumnarCatalogReader(CopyOf(result.SavedPath));
        Assert.That(reader.Description, Is.EqualTo("holiday photos"));
        Assert.That(reader.FollowJunctions, Is.True);
    }

    // The session holds the mapping, so inspect a copy.
    private string CopyOf(string path)
    {
        var copy = Path.Combine(_dir, "copy-" + Guid.NewGuid().ToString("N") + ".cdex");
        using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var dst = File.Create(copy))
            src.CopyTo(dst);
        return copy;
    }

    [Test]
    public async Task Create_without_follow_junctions_stores_false()
    {
        var s = LoadFiles();
        await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "j"));
        Assert.That(s.GetCatalogs().Single().FollowJunctions, Is.False);
    }

    [Test]
    public async Task Create_reports_scanning_writing_and_saving_progress()
    {
        var s = LoadFiles();
        var cap = new Capture();
        await s.CreateCatalogAsync(Req(MakeData("d1", "a.txt")), cap);
        var phases = cap.Reports.Select(r => r.Phase).Distinct().ToList();
        Assert.That(phases, Is.EqualTo(new[] { "scanning", "writing", "saving" }));
    }

    [Test]
    public async Task Create_never_overwrites_an_existing_file()
    {
        var p1 = ScanToCdex(MakeData("d1", "a.txt"), "taken.cdex");
        var s = LoadFiles();
        var bytes = File.ReadAllBytes(p1);

        var ex = await Assert.ThrowsAsync<CatalogFileExistsException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d2", "z.txt"), "taken")));

        Assert.That(ex!.ExistingCatalogId, Is.EqualTo(0), "the existing file is a loaded catalog: Rescan existing is possible");
        Assert.That(File.ReadAllBytes(p1), Is.EqualTo(bytes));
        Assert.That(s.CatalogCount, Is.EqualTo(1));
        Assert.That(Directory.GetFiles(_cat, "*.tmp"), Is.Empty);
    }

    [Test]
    public async Task Create_refuses_an_existing_file_that_is_not_loaded_without_an_existing_catalog_id()
    {
        var s = LoadFiles();
        File.WriteAllText(Path.Combine(_cat, "stray.cdex"), "not a catalog");

        var ex = await Assert.ThrowsAsync<CatalogFileExistsException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "stray")));

        Assert.That(ex!.ExistingCatalogId, Is.Null);
        Assert.That(File.ReadAllText(Path.Combine(_cat, "stray.cdex")), Is.EqualTo("not a catalog"));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Create_never_overwrites_a_file_that_appears_just_before_the_rename()
    {
        var s = LoadFiles();
        var target = Path.Combine(Path.GetFullPath(_cat), "race.cdex");
        var cap = new Capture
        {
            OnReport = r =>
            {
                if (r.Phase == "saving") File.WriteAllText(target, "someone else won the race");
            }
        };

        await Assert.ThrowsAsync<CatalogFileExistsException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "race"), cap));

        Assert.That(File.ReadAllText(target), Is.EqualTo("someone else won the race"));
        Assert.That(Directory.GetFiles(_cat, "*.tmp"), Is.Empty);
        Assert.That(s.CatalogCount, Is.EqualTo(0));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cancel_leaves_no_files_and_no_session_change()
    {
        var data = MakeData("d1");
        for (var i = 0; i < 2500; i++) File.WriteAllText(Path.Combine(data, $"f{i}.txt"), "");
        var s = LoadFiles();
        using var cts = new CancellationTokenSource();
        var cap = new Capture { OnReport = r => { if (r.EntriesScanned >= 1000) cts.Cancel(); } };

        await Assert.ThatAsync(() => s.CreateCatalogAsync(Req(data, "c"), cap, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty);
        Assert.That(s.CatalogCount, Is.EqualTo(0));
        Assert.That(s.AnyCatalogJobRunning, Is.False);
    }

    [Test]
    public async Task A_cancel_that_surfaces_wrapped_from_the_scan_is_still_a_cancellation()
    {
        var s = LoadFiles();
        using var cts = new CancellationTokenSource();
        // The scan's progress callback can run on traversal workers, where a cancel may arrive wrapped.
        var cap = new Capture
        {
            OnReport = r =>
            {
                if (r.Phase != "scanning") return;
                cts.Cancel();
                throw new AggregateException(new InvalidOperationException("wrapped by the traversal"));
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "c"), cap, cts.Token));

        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty);
        Assert.That(s.CatalogCount, Is.EqualTo(0));
    }

    [Test]
    public async Task A_failed_load_does_not_move_the_working_folder()
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(new List<string>());
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await _session.LoadAsync(_cat);
        Assert.That(_session.CatalogDir, Is.EqualTo(Path.GetFullPath(_cat)));

        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_ => throw new DirectoryNotFoundException("mistyped"));
        await Assert.ThatAsync(() => _session.LoadAsync(Path.Combine(_dir, "mistyped")), Throws.Exception);

        Assert.That(_session.CatalogDir, Is.EqualTo(Path.GetFullPath(_cat)));
        Assert.That(_session.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d1", "a.txt") }).CatalogDir,
            Is.EqualTo(Path.GetFullPath(_cat)));
    }

    [Test]
    public async Task A_load_that_never_succeeded_leaves_no_working_folder()
    {
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_ => throw new DirectoryNotFoundException("mistyped"));
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await Assert.ThatAsync(() => _session.LoadAsync(_cat), Throws.Exception);
        Assert.That(_session.CatalogDir, Is.Null);
    }

    [Test]
    public async Task Cancel_just_before_the_rename_leaves_no_files_and_no_session_change()
    {
        var s = LoadFiles();
        using var cts = new CancellationTokenSource();
        var cap = new Capture { OnReport = r => { if (r.Phase == "saving") cts.Cancel(); } };

        await Assert.ThatAsync(() => s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "c"), cap, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty);
        Assert.That(s.CatalogCount, Is.EqualTo(0));
    }

    [Test]
    public async Task A_failure_after_the_rename_removes_the_new_file_and_changes_nothing()
    {
        ScanToCdex(MakeData("d1", "a.txt"), "one.cdex");
        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_ => Directory.GetFiles(_cat, "*.cdex").ToList());
        var fail = false;
        var s = Load(loader, path =>
        {
            if (fail) throw new IOException("cannot map");
            return new ColumnarCatalogReader(path);
        });
        fail = true;

        await Assert.ThrowsAsync<IOException>(() => s.CreateCatalogAsync(Req(MakeData("d2", "z.txt"), "two")));

        Assert.That(Directory.GetFiles(_cat), Has.Length.EqualTo(1));
        Assert.That(s.CatalogCount, Is.EqualTo(1));
        Assert.That(s.AnyCatalogJobRunning, Is.False);
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_missing_root_fails_before_writing_anything()
    {
        var s = LoadFiles();
        await Assert.ThrowsAsync<RootNotAccessibleException>(() =>
            s.CreateCatalogAsync(Req(Path.Combine(_dir, "nope"), "x")));
        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty);
        await Task.CompletedTask;
    }

    [Test]
    public async Task An_invalid_file_name_is_refused()
    {
        var s = LoadFiles();
        await Assert.ThrowsAsync<InvalidCatalogFileNameException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), @"..\evil")));
        Assert.That(Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName), Is.EquivalentTo(new[] { "cat", "d1" }));
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_missing_working_folder_is_reported_as_not_writable()
    {
        var s = LoadFiles();
        Directory.Delete(_cat, true);
        await Assert.ThrowsAsync<CatalogFolderNotWritableException>(() =>
            s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "x")));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Create_before_anything_was_loaded_has_no_working_folder()
    {
        _session = new CatalogSession(Substitute.For<ILoadCatalogService>(), Substitute.For<ILogger>());
        Assert.That(_session.CatalogDir, Is.Null);
        await Assert.ThrowsAsync<NoCatalogDirException>(() => _session.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "x")));
        await Task.CompletedTask;
    }

    private sealed class BlockingLoader : ILoadCatalogService
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        public bool Block;

        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath)
        {
            if (Block)
            {
                Entered.Set();
                Release.Wait();
            }

            return [];
        }
    }

    [Test]
    public async Task Create_is_refused_during_a_load_and_leaves_no_trace()
    {
        var loader = new BlockingLoader();
        Load(loader);
        loader.Block = true;
        var load = Task.Run(() => _session.LoadAsync(_cat));
        loader.Entered.Wait();
        try
        {
            Assert.That(_session.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d1", "a.txt") }).Busy, Is.True);
            await Assert.ThatAsync(() => _session.CreateCatalogAsync(Req(MakeData("d2", "a.txt"), "x")),
                Throws.TypeOf<CatalogJobBusyException>());
            Assert.That(_session.AnyCatalogJobRunning, Is.False, "a refused create must not leave the job counter set");
        }
        finally
        {
            loader.Release.Set();
        }

        await load;
        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty);
    }

    [Test]
    public async Task A_load_is_refused_while_a_create_runs_and_the_new_file_is_picked_up_by_the_next_load()
    {
        var data = MakeData("d1");
        for (var i = 0; i < 1000; i++) File.WriteAllText(Path.Combine(data, $"f{i}.txt"), "");
        var s = LoadFiles();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var cap = new Capture { OnReport = r => { if (r.Phase == "saving") { entered.Set(); release.Wait(); } } };

        var create = s.CreateCatalogAsync(Req(data, "later"), cap);
        entered.Wait();
        try
        {
            Assert.That(s.AnyCatalogJobRunning, Is.True);
            await Assert.ThatAsync(() => s.LoadAsync(_cat), Throws.TypeOf<InvalidOperationException>());
        }
        finally
        {
            release.Set();
        }

        await create;
        Assert.That(s.AnyCatalogJobRunning, Is.False);

        await s.LoadAsync(_cat);
        Assert.That(s.CatalogCount, Is.EqualTo(1));
        Assert.That(s.GetCatalogs().Single().SourcePath, Does.EndWith("later.cdex"));
        Assert.That(s.GetChildren(new EntryRefDto(0, 0)), Has.Count.EqualTo(1000));
    }

    [Test]
    public async Task Several_creates_can_run_at_once_with_distinct_ids_but_not_for_the_same_file()
    {
        var s = LoadFiles();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var cap = new Capture { OnReport = r => { if (r.Phase == "saving") { entered.Set(); release.Wait(); } } };
        var first = s.CreateCatalogAsync(Req(MakeData("d1", "a.txt"), "one"), cap);
        entered.Wait();
        try
        {
            Assert.That(s.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d3", "a.txt"), FileName = "one" }).Exists,
                Is.True, "an in-flight target counts as existing");
            await Assert.ThrowsAsync<CatalogFileExistsException>(() => s.CreateCatalogAsync(Req(MakeData("d2", "b.txt"), "one")));
            var second = await s.CreateCatalogAsync(Req(MakeData("d4", "b.txt"), "two"));
            Assert.That(second.CatalogId, Is.EqualTo(0));
        }
        finally
        {
            release.Set();
        }

        var firstResult = await first;
        Assert.That(firstResult.CatalogId, Is.EqualTo(1));
        Assert.That(s.GetCatalogs().Select(c => c.CatalogId), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public async Task A_rescan_and_a_create_can_run_together()
    {
        var d1 = MakeData("d1", "a.txt");
        ScanToCdex(d1, "one.cdex");
        var s = LoadFiles();
        File.WriteAllText(Path.Combine(d1, "b.txt"), "new");

        var rescan = s.RescanAsync(0);
        var create = s.CreateCatalogAsync(Req(MakeData("d2", "z.txt"), "two"));
        await Task.WhenAll(rescan, create);

        Assert.That(s.CatalogCount, Is.EqualTo(2));
        Assert.That(RootNames(s, 0), Is.EqualTo(new[] { "a.txt", "b.txt" }));
        Assert.That(RootNames(s, 1), Is.EqualTo(new[] { "z.txt" }));
        // Roots must reflect both the swapped catalog 0 and the new catalog 1 (no lost update).
        Assert.That(s.Roots.Count, Is.EqualTo(2));
        Assert.That(s.ResolveFullPath(new EntryRefDto(1, 0)), Does.EndWith("d2"));
    }

    // ---- preflight ----

    [Test]
    public async Task Preflight_reports_default_name_dir_and_clean_state()
    {
        var s = LoadFiles();
        var data = MakeData("d1", "a.txt");

        var pf = s.PreflightCreate(new CreatePreflightRequest { RootPath = data });

        Assert.That(pf.DefaultFileName, Is.EqualTo(CatalogNaming.DefaultFileName(data)));
        Assert.That(pf.FileName, Is.EqualTo(pf.DefaultFileName));
        Assert.That(pf.CatalogDir, Is.EqualTo(Path.GetFullPath(_cat)).IgnoreCase);
        Assert.That(pf.TargetPath, Is.EqualTo(Path.Combine(Path.GetFullPath(_cat), pf.DefaultFileName + ".cdex")).IgnoreCase);
        Assert.That(pf.Exists, Is.False);
        Assert.That(pf.Writable, Is.True);
        Assert.That(pf.RootAccessible, Is.True);
        Assert.That(pf.FileNameError, Is.Null);
        Assert.That(pf.SameRootCatalogIds, Is.Empty);
        Assert.That(pf.Busy, Is.False);
        Assert.That(Directory.GetFileSystemEntries(_cat), Is.Empty, "the writable probe leaves nothing behind");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Preflight_reports_an_existing_loaded_file_and_the_same_root_catalogs()
    {
        var data = MakeData("d1", "a.txt");
        ScanToCdex(data, "taken.cdex");
        var s = LoadFiles();

        var pf = s.PreflightCreate(new CreatePreflightRequest { RootPath = data, FileName = "Taken" });

        Assert.That(pf.Exists, Is.True);
        Assert.That(pf.ExistingCatalogId, Is.EqualTo(0));
        Assert.That(pf.SameRootCatalogIds, Is.EqualTo(new[] { 0 }));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Preflight_reports_an_existing_file_that_is_not_loaded()
    {
        var s = LoadFiles();
        File.WriteAllText(Path.Combine(_cat, "stray.cdex"), "x");
        var pf = s.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d1", "a.txt"), FileName = "stray" });
        Assert.That(pf.Exists, Is.True);
        Assert.That(pf.ExistingCatalogId, Is.Null);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Preflight_reports_bad_names_unwritable_folders_and_unreachable_roots()
    {
        var s = LoadFiles();
        var bad = s.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d1", "a.txt"), FileName = "a/b" });
        Assert.That(bad.FileNameError, Is.Not.Null);

        var missingRoot = s.PreflightCreate(new CreatePreflightRequest { RootPath = Path.Combine(_dir, "nope") });
        Assert.That(missingRoot.RootAccessible, Is.False);
        Assert.That(missingRoot.DefaultFileName, Is.Not.Null.And.Not.Empty);

        Directory.Delete(_cat, true);
        var noDir = s.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d2", "a.txt") });
        Assert.That(noDir.Writable, Is.False);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Preflight_without_a_loaded_folder_reports_no_dir_and_not_writable()
    {
        _session = new CatalogSession(Substitute.For<ILoadCatalogService>(), Substitute.For<ILogger>());
        var pf = _session.PreflightCreate(new CreatePreflightRequest { RootPath = MakeData("d1", "a.txt") });
        Assert.That(pf.CatalogDir, Is.Null);
        Assert.That(pf.Writable, Is.False);
        Assert.That(pf.TargetPath, Is.Null);
        await Task.CompletedTask;
    }
}
