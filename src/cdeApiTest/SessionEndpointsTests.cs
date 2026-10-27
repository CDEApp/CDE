using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives the <see cref="SessionEndpoints"/> handlers against a real one-catalog session.</summary>
[TestFixture]
public class SessionEndpointsTests
{
    private TestCatalog _catalog;
    private readonly ApiOptions _options = new() { ConfigPath = "configured" };

    [SetUp]
    public void SetUp() => _catalog = new TestCatalog();

    [TearDown]
    public void TearDown() => _catalog.Dispose();

    [Test]
    public void Health_is_ok()
    {
        Assert.That(((IStatusCodeHttpResult)SessionEndpoints.Health()).StatusCode, Is.EqualTo(200));
    }

    [Test]
    public void Catalogs_reports_the_loaded_catalogs_and_status_figures()
    {
        var response = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session)).Value!;

        Assert.That(response.Catalogs, Has.Count.EqualTo(1));
        Assert.That(response.CatalogsLoaded, Is.EqualTo(1));
        Assert.That(response.TotalEntries, Is.EqualTo(_catalog.Session.TotalEntryCount).And.GreaterThan(0));
        Assert.That(response.CatalogDir, Is.EqualTo(_catalog.Session.CatalogDir));
        Assert.That(response.MemoryBytes, Is.GreaterThan(0));
    }

    [TestCase(null, "configured", TestName = "no path uses the configured folder")]
    [TestCase("?path=", "configured", TestName = "empty path uses the configured folder")]
    [TestCase("?path=%20%20", "configured", TestName = "blank path uses the configured folder")]
    [TestCase("?path=elsewhere", "elsewhere", TestName = "path loads that folder")]
    public async Task Reload_streams_done_after_loading_from_the_chosen_folder(string query, string expectedPath)
    {
        var (ctx, body) = TestCatalog.NewContext(query);

        await SessionEndpoints.ReloadAsync(ctx, _catalog.Session, _options);

        Assert.That(_catalog.Loader.RequestedPaths.Last(), Is.EqualTo(expectedPath));
        var done = TestCatalog.Events(TestCatalog.Text(body)).Last();
        Assert.That(done.Event, Is.EqualTo("done"));
        Assert.That(done.Data.GetProperty("catalogs").GetInt32(), Is.EqualTo(1));
        Assert.That(done.Data.GetProperty("entries").GetInt64(), Is.EqualTo(_catalog.Session.TotalEntryCount));
    }

    [Test]
    public async Task Reload_is_refused_while_a_rescan_runs()
    {
        // Hold a read lease so the rescan waits at its final swap, keeping it running.
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var lease = _catalog.Session.AcquireRead(0);
            acquired.Set();
            release.Wait();
        });
        acquired.Wait();
        var (rescanCtx, _) = TestCatalog.NewContext();
        var rescan = RescanEndpoint.HandleAsync(rescanCtx, 0, _catalog.Session);
        while (!_catalog.Session.AnyCatalogJobRunning) await Task.Delay(10);
        var loadsBefore = _catalog.Loader.RequestedPaths.Count;

        var (ctx, body) = TestCatalog.NewContext();
        await SessionEndpoints.ReloadAsync(ctx, _catalog.Session, _options);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(TestCatalog.Text(body), Is.EqualTo("a rescan or a new catalog is running"));
        Assert.That(_catalog.Loader.RequestedPaths, Has.Count.EqualTo(loadsBefore));
        release.Set();
        await holder;
        await rescan.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
