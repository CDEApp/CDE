using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Jobs;
using cdeAppCore.Session;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace cdeApiTest;

[TestFixture]
public class EndpointFailureLoggingTests
{
    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private ListSink _sink;
    private Serilog.ILogger _logger;
    private readonly InvalidOperationException _boom = new("boom");

    [SetUp]
    public void SetUp()
    {
        _sink = new ListSink();
        _logger = new LoggerConfiguration().WriteTo.Sink(_sink).CreateLogger();
    }

    private void AssertLoggedWithException()
    {
        Assert.That(_sink.Events, Has.Count.EqualTo(1));
        Assert.That(_sink.Events[0].Level, Is.EqualTo(LogEventLevel.Error));
        Assert.That(_sink.Events[0].Exception, Is.SameAs(_boom));
    }

    private static ICatalogSession SessionWithOneCatalog()
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightRescan(0).Returns(RescanPreflight.Ready);
        session.GetCatalogs().Returns([new CatalogInfoDto { CatalogId = 0, RootPath = "r", SourcePath = "x.cdex" }]);
        session.PreflightCreate(Arg.Any<CreatePreflightRequest>()).Returns(new CreatePreflightResult
        {
            CatalogDir = "cat", Writable = true, RootAccessible = true, FileName = "x", TargetPath = "x.cdex"
        });
        return session;
    }

    private static async Task<JobDto> RunToEnd(JobRegistry jobs, JobSubmitRequest request)
    {
        var job = jobs.Submit(request).Job;
        Assert.That(job, Is.Not.Null);
        return await TestCatalog.WaitForJob(jobs, job.Id, j => j.IsTerminal);
    }

    [Test]
    public async Task A_failed_rescan_job_is_logged_with_the_exception_and_still_reported_as_failed()
    {
        var session = SessionWithOneCatalog();
        session.RescanAsync(0, Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>()).Returns<RescanResult>(_ => throw _boom);
        using var jobs = new JobRegistry(session, logger: _logger);

        var job = await RunToEnd(jobs, new JobSubmitRequest { Kind = JobKind.Rescan, CatalogId = 0 });

        Assert.That(job.Error.Code, Is.EqualTo(JobErrorCodes.RescanFailed));
        AssertLoggedWithException();
    }

    [Test]
    public async Task A_failed_create_job_is_logged_with_the_exception_and_still_reported_as_failed()
    {
        var session = SessionWithOneCatalog();
        session.CreateCatalogAsync(Arg.Any<CreateCatalogRequest>(), Arg.Any<IProgress<CreateProgress>>(), Arg.Any<CancellationToken>())
            .Returns<CreateCatalogResult>(_ => throw _boom);
        using var jobs = new JobRegistry(session, logger: _logger);

        var job = await RunToEnd(jobs, new JobSubmitRequest { Kind = JobKind.Create, RootPath = "r", FileName = "x" });

        Assert.That(job.Error.Code, Is.EqualTo(JobErrorCodes.CreateFailed));
        AssertLoggedWithException();
    }

    [Test]
    public async Task An_expected_refusal_during_a_create_job_is_not_logged_as_an_error()
    {
        var session = SessionWithOneCatalog();
        session.CreateCatalogAsync(Arg.Any<CreateCatalogRequest>(), Arg.Any<IProgress<CreateProgress>>(), Arg.Any<CancellationToken>())
            .Returns<CreateCatalogResult>(_ => throw new CatalogJobBusyException("busy"));
        using var jobs = new JobRegistry(session, logger: _logger);

        var job = await RunToEnd(jobs, new JobSubmitRequest { Kind = JobKind.Create, RootPath = "r", FileName = "x" });

        Assert.That(job.Error.Code, Is.EqualTo(JobErrorCodes.Busy));
        Assert.That(_sink.Events, Is.Empty);
    }

    [Test]
    public async Task A_failed_migrate_is_logged_with_the_exception_and_still_sent_to_the_client()
    {
        var session = Substitute.For<ICatalogSession>();
        session.LegacyCatalogs.Returns([new LegacyCatalogDto { Path = "a.cde", Writable = true }]);
        session.MigrateLegacyCatalogsAsync(Arg.Any<IProgress<MigrateProgress>>(), Arg.Any<CancellationToken>())
            .Returns<MigrateResult>(_ => throw _boom);
        var (ctx, body) = TestCatalog.NewContext();

        using var jobs = new JobRegistry(session);

        await MigrateEndpoint.HandleAsync(ctx, session, jobs, _logger);

        Assert.That(TestCatalog.Text(body), Does.Contain(MigrateEndpoint.Failed));
        AssertLoggedWithException();
    }
}
