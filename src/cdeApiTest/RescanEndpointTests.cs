using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// Drives <see cref="RescanEndpoint"/> against a real <see cref="CatalogSession"/> over temp folders, with a
/// <see cref="DefaultHttpContext"/> writing to a memory stream. Not covered here: Kestrel, the handshake-token
/// middleware and a real client disconnect (RequestAborted is simulated with a cancellation token).
/// </summary>
[TestFixture]
public class RescanEndpointTests
{
    private string _dir;
    private string _data;
    private string _cdex;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-api-rescan-" + Guid.NewGuid().ToString("N"));
        _data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(_data);
        File.WriteAllText(Path.Combine(_data, "a.txt"), "a");

        var re = new RootEntry();
        re.PopulateRoot(_data);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        _cdex = Path.Combine(_dir, "one.cdex");
        re.ActualFileName = _cdex;
        ColumnarFormat.Write(re, _cdex);

        _session = new CatalogSession(new FixedLoader(_cdex), null);
        _session.LoadAsync("x").GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private static (DefaultHttpContext ctx, MemoryStream body) NewContext(CancellationToken aborted = default)
    {
        var body = new MemoryStream();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = body;
        ctx.RequestAborted = aborted;
        return (ctx, body);
    }

    private static string Text(MemoryStream body) => Encoding.UTF8.GetString(body.ToArray());

    private static List<(string Event, JsonElement Data)> Events(string sse) =>
        sse.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(frame =>
        {
            var lines = frame.Split('\n');
            return (lines[0]["event: ".Length..], JsonDocument.Parse(lines[1]["data: ".Length..]).RootElement.Clone());
        }).ToList();

    [Test]
    public async Task Streams_progress_then_done_and_replaces_the_catalog()
    {
        File.WriteAllText(Path.Combine(_data, "b.txt"), "b");
        var (ctx, body) = NewContext();

        await RescanEndpoint.HandleAsync(ctx, 0, _session);

        var events = Events(Text(body));
        Assert.That(ctx.Response.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(events.Select(e => e.Event), Does.Contain("progress"));
        Assert.That(events.Last().Event, Is.EqualTo("done"));
        Assert.That(events.Count(e => e.Event is "done" or "error"), Is.EqualTo(1));
        var done = events.Last().Data;
        Assert.That(done.GetProperty("catalogId").GetInt32(), Is.EqualTo(0));
        Assert.That(done.GetProperty("entries").GetInt64(), Is.EqualTo(2));
        Assert.That(_session.TotalEntryCount, Is.EqualTo(2));
        Assert.That(events.First(e => e.Event == "progress").Data.GetProperty("phase").GetString(), Is.EqualTo("scanning"));
    }

    [Test]
    public async Task Unreachable_root_is_refused_before_the_stream_with_a_distinct_error()
    {
        Directory.Delete(_data, true);
        var (ctx, body) = NewContext();

        await RescanEndpoint.HandleAsync(ctx, 0, _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        Assert.That(ctx.Response.ContentType, Does.StartWith("application/json"));
        var json = JsonDocument.Parse(Text(body)).RootElement;
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("root-not-accessible"));
        Assert.That(json.GetProperty("rootPath").GetString(), Is.EqualTo(_session.GetCatalogs()[0].RootPath));
        Assert.That(json.GetProperty("message").GetString(), Does.Contain("can't be reached"));
    }

    [Test]
    public async Task Unknown_catalog_is_404()
    {
        var (ctx, body) = NewContext();
        await RescanEndpoint.HandleAsync(ctx, 9, _session);
        Assert.That(ctx.Response.StatusCode, Is.EqualTo(404));
        Assert.That(JsonDocument.Parse(Text(body)).RootElement.GetProperty("code").GetString(), Is.EqualTo("unknown-catalog"));
    }

    [Test]
    public async Task A_second_rescan_of_the_same_catalog_is_409_while_the_first_runs()
    {
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var lease = _session.AcquireRead(0); // keeps the first rescan waiting at the swap
            acquired.Set();
            release.Wait();
        });
        acquired.Wait();
        var (ctx1, _) = NewContext();
        var first = RescanEndpoint.HandleAsync(ctx1, 0, _session);
        while (!_session.IsRescanning(0)) await Task.Delay(10);

        var (ctx2, body2) = NewContext();
        await RescanEndpoint.HandleAsync(ctx2, 0, _session);

        Assert.That(ctx2.Response.StatusCode, Is.EqualTo(409));
        Assert.That(JsonDocument.Parse(Text(body2)).RootElement.GetProperty("code").GetString(), Is.EqualTo("already-running"));
        release.Set();
        await holder;
        await first.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Client_abort_cancels_the_rescan_and_leaves_the_catalog_untouched()
    {
        for (var i = 0; i < 1500; i++) File.WriteAllText(Path.Combine(_data, $"f{i}.txt"), "");
        var bytes = File.ReadAllBytes(_cdex);
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var (ctx, body) = NewContext(aborted.Token);

        await RescanEndpoint.HandleAsync(ctx, 0, _session);

        Assert.That(Events(Text(body)).Select(e => e.Event), Does.Not.Contain("done"));
        Assert.That(File.ReadAllBytes(_cdex), Is.EqualTo(bytes));
        Assert.That(Directory.GetFiles(_dir, "*.tmp*"), Is.Empty);
        Assert.That(_session.IsRescanning(0), Is.False);
        Assert.That(_session.TotalEntryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task A_catalog_without_a_source_file_is_refused_with_no_source_file()
    {
        var tree = new RootEntry();
        tree.PopulateRoot(_data);
        tree.SetSummaryFields();
        tree.ActualFileName = null;
        using var legacy = new CatalogSession(new TreeLoader(tree), null);
        await legacy.LoadAsync("x");
        var (ctx, body) = NewContext();

        await RescanEndpoint.HandleAsync(ctx, 0, legacy);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        Assert.That(JsonDocument.Parse(Text(body)).RootElement.GetProperty("code").GetString(), Is.EqualTo("no-source-file"));
    }

    private sealed class TreeLoader(RootEntry tree) : ILoadCatalogService
    {
        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry> { tree });

        public IList<string> GetColumnarFiles(string configPath) => [];
    }

    [Test]
    public void SseQueue_writes_error_and_rescan_progress_events()
    {
        var pipe = new TestPipeWriter();
        using var w = new SseWriter(pipe);
        SseQueue.WriteItem(w, new cdeAppCore.Dtos.RescanProgress(2, "scanning", 1000, @"C:\x"));
        SseQueue.WriteItem(w, new cdeAppCore.Dtos.RescanError { Code = "rescan-failed", Message = "disk full" });
        w.FlushAsync().AsTask().GetAwaiter().GetResult();
        Assert.That(pipe.Text, Is.EqualTo(
            "event: progress\ndata: {\"catalogId\":2,\"phase\":\"scanning\",\"entriesScanned\":1000,\"currentPath\":\"C:\\\\x\"}\n\n" +
            "event: error\ndata: {\"code\":\"rescan-failed\",\"message\":\"disk full\",\"rootPath\":null}\n\n"));
    }

    private sealed class FixedLoader(string cdex) : ILoadCatalogService
    {
        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath) => [cdex];
    }
}
