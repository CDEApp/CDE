using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Jobs;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives the <see cref="SessionEndpoints"/> handlers against a real one-catalog session.</summary>
[TestFixture]
public class SessionEndpointsTests
{
    private TestCatalog _catalog;
    private readonly ApiOptions _options = new() { ConfigPath = "configured" };

    private JobRegistry _jobs;

    [SetUp]
    public void SetUp()
    {
        _catalog = new TestCatalog();
        _jobs = new JobRegistry(_catalog.Session);
    }

    [TearDown]
    public void TearDown()
    {
        _jobs.Dispose();
        _catalog.Dispose();
    }

    [Test]
    public void Health_is_ok()
    {
        Assert.That(((IStatusCodeHttpResult)SessionEndpoints.Health()).StatusCode, Is.EqualTo(200));
    }

    [Test]
    public void Catalogs_reports_the_loaded_catalogs_and_status_figures()
    {
        var response = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session, Config())).Value!;

        Assert.That(response.Catalogs, Has.Count.EqualTo(1));
        Assert.That(response.CatalogsLoaded, Is.EqualTo(1));
        Assert.That(response.TotalEntries, Is.EqualTo(_catalog.Session.TotalEntryCount).And.GreaterThan(0));
        Assert.That(response.CatalogDir, Is.EqualTo(_catalog.Session.CatalogDir));
        Assert.That(response.MemoryBytes, Is.GreaterThan(0));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new System.Collections.Generic.KeyValuePair<string, string>(v.Key, v.Value))).Build();

    [Test]
    public void Catalogs_defaults_to_a_30_day_stale_threshold_with_startup_rescan_off()
    {
        var response = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session, Config())).Value!;

        Assert.That(response.StaleAfterDays, Is.EqualTo(30));
        Assert.That(response.RescanStaleOnStartup, Is.False);
    }

    [Test]
    public void Catalogs_reports_the_configured_stale_settings_on_every_request()
    {
        var config = Config(("AppConfig:Scan:StaleAfterDays", "7"), ("AppConfig:Scan:RescanStaleOnStartup", "true"));
        var first = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session, config)).Value!;
        config["AppConfig:Scan:StaleAfterDays"] = "0";
        var second = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session, config)).Value!;

        Assert.That(first.StaleAfterDays, Is.EqualTo(7));
        Assert.That(first.RescanStaleOnStartup, Is.True);
        Assert.That(second.StaleAfterDays, Is.EqualTo(0));
    }

    [Test]
    public void Catalogs_treats_a_negative_threshold_as_off()
    {
        var response = ((JsonHttpResult<CatalogsResponse>)SessionEndpoints.Catalogs(_catalog.Session, Config(("AppConfig:Scan:StaleAfterDays", "-5")))).Value!;

        Assert.That(response.StaleAfterDays, Is.EqualTo(0));
    }

    [TestCase(null, "configured", TestName = "no path uses the configured folder")]
    [TestCase("?path=", "configured", TestName = "empty path uses the configured folder")]
    [TestCase("?path=%20%20", "configured", TestName = "blank path uses the configured folder")]
    [TestCase("?path=elsewhere", "elsewhere", TestName = "path loads that folder")]
    public async Task Reload_streams_done_after_loading_from_the_chosen_folder(string query, string expectedPath)
    {
        var (ctx, body) = TestCatalog.NewContext(query);

        await SessionEndpoints.ReloadAsync(ctx, _catalog.Session, _options, _jobs);

        Assert.That(_catalog.Loader.RequestedPaths.Last(), Is.EqualTo(expectedPath));
        var done = TestCatalog.Events(TestCatalog.Text(body)).Last();
        Assert.That(done.Event, Is.EqualTo("done"));
        Assert.That(done.Data.GetProperty("catalogs").GetInt32(), Is.EqualTo(1));
        Assert.That(done.Data.GetProperty("entries").GetInt64(), Is.EqualTo(_catalog.Session.TotalEntryCount));
    }

    [Test]
    public async Task Reload_is_refused_while_a_job_is_running_or_queued()
    {
        var loadsBefore = _catalog.Loader.RequestedPaths.Count;
        long id;
        using (new TestCatalog.SwapBlockingLease(_catalog.Session, 0))
        {
            id = _jobs.Submit(new JobSubmitRequest { Kind = JobKind.Rescan, CatalogId = 0 }).Job.Id;
            var (ctx, body) = TestCatalog.NewContext();

            await SessionEndpoints.ReloadAsync(ctx, _catalog.Session, _options, _jobs);

            Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(TestCatalog.Text(body), Does.StartWith("busy"));
            Assert.That(_catalog.Loader.RequestedPaths, Has.Count.EqualTo(loadsBefore));
        }

        await TestCatalog.WaitForJob(_jobs, id, j => j.IsTerminal);
    }

    [Test]
    public async Task No_job_can_be_submitted_while_a_reload_runs()
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightRescan(0).Returns(RescanPreflight.Ready);
        session.GetCatalogs().Returns([new CatalogInfoDto { CatalogId = 0, RootPath = "r", SourcePath = _catalog.Cdex }]);
        using var jobs = new JobRegistry(session);
        JobError duringLoad = null;
        session.LoadAsync(Arg.Any<string>(), Arg.Any<IProgress<CatalogLoadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                duringLoad = jobs.Submit(new JobSubmitRequest { Kind = JobKind.Rescan, CatalogId = 0 }).Refusal;
                return Task.CompletedTask;
            });
        var (ctx, _) = TestCatalog.NewContext();

        await SessionEndpoints.ReloadAsync(ctx, session, _options, jobs);

        Assert.That(duringLoad?.Code, Is.EqualTo(JobErrorCodes.Busy));
        Assert.That(jobs.GetJobs(), Is.Empty);
        Assert.That(jobs.TryBeginExclusive(), Is.Not.Null, "the hold is released when the reload ends");
    }
}
