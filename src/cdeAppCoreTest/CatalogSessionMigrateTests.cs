using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NUnit.Framework;
using Serilog.Core;

namespace cdeAppCoreTest;

/// <summary>
/// Legacy <c>.cde</c> catalogs with <see cref="CatalogSession.LoadLegacyCatalogs"/> off (the cde-tauri sidecar): they
/// are listed rather than loaded, and <see cref="CatalogSession.MigrateLegacyCatalogsAsync"/> converts them.
/// </summary>
[TestFixture]
public class CatalogSessionMigrateTests
{
    private string _dir;
    private string _data;
    private readonly List<CatalogSession> _sessions = [];

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-session-migrate-test").FullName;
        _data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(_data);
        File.WriteAllText(Path.Combine(_data, "a.txt"), "a");
    }

    [TearDown]
    public void TearDown()
    {
        _sessions.ForEach(s => s.Dispose());
        Directory.Delete(_dir, recursive: true);
    }

    private CatalogSession Session(bool loadLegacy)
    {
        var session = new CatalogSession(new LoadCatalogService(Logger.None, includeCurrentDirectory: false), null)
        {
            LoadLegacyCatalogs = loadLegacy
        };
        _sessions.Add(session);
        return session;
    }

    private string File_(string name) => Path.Combine(_dir, name);

    private async Task<string> WriteLegacyAsync(string name)
    {
        var re = new RootEntry();
        re.PopulateRoot(_data);
        re.ActualFileName = File_(name);
        using var repo = new CatalogRepository(Logger.None);
        await repo.Save(re);
        return re.ActualFileName;
    }

    private string WriteColumnar(string name)
    {
        var re = new RootEntry();
        re.PopulateRoot(_data);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        ColumnarFormat.Write(re, File_(name));
        return File_(name);
    }

    // ---- listing instead of loading ----

    [Test]
    public async Task Legacy_catalogs_are_listed_not_loaded()
    {
        var cde = await WriteLegacyAsync("a.cde");
        var session = Session(loadLegacy: false);

        await session.LoadAsync(_dir);

        Assert.That(session.CatalogCount, Is.EqualTo(0));
        var legacy = session.LegacyCatalogs.Single();
        Assert.That(legacy.Path, Is.EqualTo(cde));
        Assert.That(legacy.SizeBytes, Is.EqualTo(new FileInfo(cde).Length));
        Assert.That(legacy.Writable, Is.True);
    }

    [Test]
    public async Task Cdex_catalogs_load_and_only_unmigrated_cde_files_are_listed()
    {
        var unmigrated = await WriteLegacyAsync("a.cde");
        await WriteLegacyAsync("b.cde");
        WriteColumnar("b.cdex");
        var session = Session(loadLegacy: false);

        await session.LoadAsync(_dir);

        Assert.That(session.CatalogCount, Is.EqualTo(1));
        Assert.That(session.LegacyCatalogs.Select(l => l.Path), Is.EqualTo(new[] { unmigrated }));
    }

    [Test]
    public async Task By_default_legacy_catalogs_load_as_before()
    {
        await WriteLegacyAsync("a.cde");
        var session = new CatalogSession(new LoadCatalogService(Logger.None, includeCurrentDirectory: false), null);
        _sessions.Add(session);

        await session.LoadAsync(_dir);

        Assert.That(session.CatalogCount, Is.EqualTo(1));
        Assert.That(session.LegacyCatalogs, Is.Empty);
    }

    // ---- migration ----

    [Test]
    public async Task Migrate_converts_the_listed_catalogs_and_reports_each_one()
    {
        var a = await WriteLegacyAsync("a.cde");
        var c = await WriteLegacyAsync("c.cde");
        var session = Session(loadLegacy: false);
        await session.LoadAsync(_dir);
        var reports = new List<MigrateProgress>();

        var result = await session.MigrateLegacyCatalogsAsync(new SyncProgress<MigrateProgress>(reports.Add));

        Assert.That(result.Migrated, Is.EqualTo(2));
        Assert.That(result.Total, Is.EqualTo(2));
        Assert.That(result.Failures, Is.Empty);
        Assert.That(result.Warnings, Is.Empty);
        Assert.That(reports.Select(r => (r.Current, r.Total)), Is.EqualTo(new[] { (1, 2), (2, 2) }));
        Assert.That(reports.Select(r => r.Path), Is.EquivalentTo(new[] { a, c }));
        Assert.That(File.Exists(CatalogMigrator.MigratedPathFor(a)) && File.Exists(CatalogMigrator.TargetPathFor(a)), Is.True);
        Assert.That(session.LegacyCatalogs, Is.Empty);

        await session.LoadAsync(_dir);
        Assert.That(session.CatalogCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Migrate_reports_a_failed_catalog_and_keeps_it_listed()
    {
        await WriteLegacyAsync("a.cde");
        var broken = File_("broken.cde");
        File.WriteAllBytes(broken, [1, 2, 3, 4, 5]);
        var session = Session(loadLegacy: false);
        await session.LoadAsync(_dir);

        var result = await session.MigrateLegacyCatalogsAsync();

        Assert.That(result.Migrated, Is.EqualTo(1));
        Assert.That(result.Failures.Select(f => f.Path), Is.EqualTo(new[] { broken }));
        Assert.That(result.Failures[0].Message, Is.Not.Empty);
        Assert.That(session.LegacyCatalogs.Select(l => l.Path), Is.EqualTo(new[] { broken }));
    }

    [Test]
    public async Task Other_catalog_jobs_are_refused_while_migrating()
    {
        await WriteLegacyAsync("a.cde");
        var session = Session(loadLegacy: false);
        await session.LoadAsync(_dir);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var migrating = Task.Run(() => session.MigrateLegacyCatalogsAsync(new SyncProgress<MigrateProgress>(_ =>
        {
            entered.Set();
            release.Wait();
        })));
        entered.Wait();

        try
        {
            Assert.That(session.AnyCatalogJobRunning, Is.True);
            Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadAsync(_dir));
            Assert.ThrowsAsync<CatalogJobBusyException>(() => session.MigrateLegacyCatalogsAsync());
            Assert.That(session.PreflightCreate(new CreatePreflightRequest { RootPath = _data }).Busy, Is.True);
        }
        finally
        {
            release.Set();
            await migrating;
        }

        Assert.That(session.AnyCatalogJobRunning, Is.False);
    }

    [Test]
    public async Task Migrate_is_refused_while_a_rescan_runs()
    {
        await WriteLegacyAsync("a.cde");
        WriteColumnar("b.cdex");
        var session = Session(loadLegacy: false);
        await session.LoadAsync(_dir);
        // Hold a read lease so the rescan waits at its final swap, keeping it running.
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var lease = session.AcquireRead(0);
            acquired.Set();
            release.Wait();
        });
        acquired.Wait();
        var rescan = session.RescanAsync(0);
        while (!session.IsRescanning(0)) await Task.Delay(10);

        try
        {
            Assert.ThrowsAsync<CatalogJobBusyException>(() => session.MigrateLegacyCatalogsAsync());
        }
        finally
        {
            release.Set();
            await holder;
            await rescan.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    // Progress<T> posts to a captured context; this one reports on the calling thread, in order.
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
