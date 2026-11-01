using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace cdeApiTest;

/// <summary>An unexpected failure is sent to the client as an SSE error event AND written to the sidecar log (#176).</summary>
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

    [Test]
    public async Task A_failed_rescan_is_logged_with_the_exception_and_still_sent_to_the_client()
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightRescan(0).Returns(RescanPreflight.Ready);
        session.RescanAsync(0, Arg.Any<IProgress<RescanProgress>>(), Arg.Any<CancellationToken>()).Returns<RescanResult>(_ => throw _boom);
        var (ctx, body) = TestCatalog.NewContext();

        await RescanEndpoint.HandleAsync(ctx, 0, session, _logger);

        Assert.That(TestCatalog.Text(body), Does.Contain(RescanEndpoint.Failed));
        AssertLoggedWithException();
    }

    [Test]
    public async Task A_failed_create_is_logged_with_the_exception_and_still_sent_to_the_client()
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightCreate(Arg.Any<CreatePreflightRequest>()).Returns(new CreatePreflightResult
        {
            CatalogDir = "cat", Writable = true, RootAccessible = true, FileName = "x", TargetPath = "x.cdex"
        });
        session.CreateCatalogAsync(Arg.Any<CreateCatalogRequest>(), Arg.Any<IProgress<CreateProgress>>(), Arg.Any<CancellationToken>())
            .Returns<CreateCatalogResult>(_ => throw _boom);
        var (ctx, body) = TestCatalog.NewContext();

        await CreateCatalogEndpoint.HandleAsync(ctx, new CreateCatalogRequest { RootPath = "r", FileName = "x" }, session, _logger);

        Assert.That(TestCatalog.Text(body), Does.Contain(CreateCatalogEndpoint.Failed));
        AssertLoggedWithException();
    }

    [Test]
    public async Task An_expected_create_refusal_is_not_logged_as_an_error()
    {
        var session = Substitute.For<ICatalogSession>();
        session.PreflightCreate(Arg.Any<CreatePreflightRequest>()).Returns(new CreatePreflightResult
        {
            CatalogDir = "cat", Writable = true, RootAccessible = true, FileName = "x", TargetPath = "x.cdex"
        });
        session.CreateCatalogAsync(Arg.Any<CreateCatalogRequest>(), Arg.Any<IProgress<CreateProgress>>(), Arg.Any<CancellationToken>())
            .Returns<CreateCatalogResult>(_ => throw new CatalogJobBusyException("busy"));
        var (ctx, _) = TestCatalog.NewContext();

        await CreateCatalogEndpoint.HandleAsync(ctx, new CreateCatalogRequest { RootPath = "r", FileName = "x" }, session, _logger);

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

        await MigrateEndpoint.HandleAsync(ctx, session, _logger);

        Assert.That(TestCatalog.Text(body), Does.Contain(MigrateEndpoint.Failed));
        AssertLoggedWithException();
    }
}
