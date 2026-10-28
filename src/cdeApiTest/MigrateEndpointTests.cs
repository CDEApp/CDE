using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore;
using cdeAppCore.Session;
using cdeLib.Catalog;
using cdeLib.Entities;
using NUnit.Framework;
using Serilog.Core;

namespace cdeApiTest;

/// <summary>Drives <see cref="MigrateEndpoint"/> against a real session that lists legacy catalogs rather than loading them.</summary>
[TestFixture]
public class MigrateEndpointTests
{
    private string _dir;
    private string _data;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-api-migrate-test").FullName;
        _data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(_data);
        File.WriteAllText(Path.Combine(_data, "a.txt"), "a");
        _session = new CatalogSession(new LoadCatalogService(Logger.None, includeCurrentDirectory: false), null)
        {
            LoadLegacyCatalogs = false
        };
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task<string> WriteLegacyAsync(string name)
    {
        var re = new RootEntry();
        re.PopulateRoot(_data);
        re.ActualFileName = Path.Combine(_dir, name);
        using var repo = new CatalogRepository(Logger.None);
        await repo.Save(re);
        return re.ActualFileName;
    }

    private static string Code(MemoryStream body) =>
        JsonDocument.Parse(TestCatalog.Text(body)).RootElement.GetProperty("code").GetString();

    [Test]
    public async Task Streams_progress_for_each_catalog_then_done()
    {
        var a = await WriteLegacyAsync("a.cde");
        var b = await WriteLegacyAsync("b.cde");
        await _session.LoadAsync(_dir);
        var (ctx, body) = TestCatalog.NewContext();

        await MigrateEndpoint.HandleAsync(ctx, _session);

        var events = TestCatalog.Events(TestCatalog.Text(body));
        var progress = events.Where(e => e.Event == "progress").Select(e => e.Data).ToList();
        Assert.That(progress.Select(p => p.GetProperty("current").GetInt32()), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(progress.Select(p => p.GetProperty("path").GetString()), Is.EquivalentTo(new[] { a, b }));
        var done = events.Last();
        Assert.That(done.Event, Is.EqualTo("done"));
        Assert.That(done.Data.GetProperty("migrated").GetInt32(), Is.EqualTo(2));
        Assert.That(done.Data.GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That(done.Data.GetProperty("failures").GetArrayLength(), Is.EqualTo(0));
        Assert.That(File.Exists(CatalogMigrator.TargetPathFor(a)) && File.Exists(CatalogMigrator.TargetPathFor(b)), Is.True);
    }

    [Test]
    public async Task A_failed_catalog_is_listed_in_the_done_report()
    {
        await WriteLegacyAsync("a.cde");
        var broken = Path.Combine(_dir, "broken.cde");
        File.WriteAllBytes(broken, [1, 2, 3, 4, 5]);
        await _session.LoadAsync(_dir);
        var (ctx, body) = TestCatalog.NewContext();

        await MigrateEndpoint.HandleAsync(ctx, _session);

        var done = TestCatalog.Events(TestCatalog.Text(body)).Last();
        Assert.That(done.Data.GetProperty("migrated").GetInt32(), Is.EqualTo(1));
        var failure = done.Data.GetProperty("failures").EnumerateArray().Single();
        Assert.That(failure.GetProperty("path").GetString(), Is.EqualTo(broken));
        Assert.That(failure.GetProperty("message").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task Nothing_to_migrate_is_refused_before_the_stream()
    {
        await _session.LoadAsync(_dir);
        var (ctx, body) = TestCatalog.NewContext();

        await MigrateEndpoint.HandleAsync(ctx, _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        Assert.That(Code(body), Is.EqualTo(MigrateEndpoint.NothingToMigrate));
    }

    [Test]
    public async Task Migration_is_refused_while_another_catalog_job_runs()
    {
        await WriteLegacyAsync("a.cde");
        TestCatalog.WriteCatalog(_data, Path.Combine(_dir, "b.cdex"));
        await _session.LoadAsync(_dir);
        // Hold a read lease so the rescan waits at its final swap, keeping it running.
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var lease = _session.AcquireRead(0);
            acquired.Set();
            release.Wait();
        });
        acquired.Wait();
        var rescan = _session.RescanAsync(0);
        while (!_session.AnyCatalogJobRunning) await Task.Delay(10);
        var (ctx, body) = TestCatalog.NewContext();

        try
        {
            await MigrateEndpoint.HandleAsync(ctx, _session);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(409));
            Assert.That(Code(body), Is.EqualTo(MigrateEndpoint.Busy));
        }
        finally
        {
            release.Set();
            await holder;
            await rescan.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
