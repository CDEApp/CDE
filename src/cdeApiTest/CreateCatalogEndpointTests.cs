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
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// Drives <see cref="CreateCatalogEndpoint"/> against a real <see cref="CatalogSession"/> over temp folders (see
/// <see cref="RescanEndpointTests"/> for what this does not cover: Kestrel, the token middleware, real disconnects).
/// </summary>
[TestFixture]
public class CreateCatalogEndpointTests
{
    private string _dir;
    private string _data;
    private string _cat;
    private string _existing;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-api-create-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        _data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(_cat);
        Directory.CreateDirectory(_data);
        File.WriteAllText(Path.Combine(_data, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_data, "b.txt"), "b");

        var other = Path.Combine(_dir, "other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "o.txt"), "o");
        var re = new RootEntry();
        re.PopulateRoot(other);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        _existing = Path.Combine(_cat, "existing.cdex");
        re.ActualFileName = _existing;
        ColumnarFormat.Write(re, _existing);

        _session = new CatalogSession(new DirLoader(), null);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private sealed class DirLoader : ILoadCatalogService
    {
        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath) =>
            Directory.Exists(configPath) ? Directory.GetFiles(configPath, "*.cdex") : [];
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

    private static JsonElement Json(MemoryStream body) => JsonDocument.Parse(Text(body)).RootElement;

    private static List<(string Event, JsonElement Data)> Events(string sse) =>
        sse.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(frame =>
        {
            var lines = frame.Split('\n');
            return (lines[0]["event: ".Length..], JsonDocument.Parse(lines[1]["data: ".Length..]).RootElement.Clone());
        }).ToList();

    private CreateCatalogRequest Req(string name = "new", string root = null) =>
        new() { RootPath = root ?? _data, FileName = name, Description = "d" };

    [Test]
    public async Task Preflight_returns_the_dialog_facts_as_camel_case_json()
    {
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.PreflightAsync(ctx, new CreatePreflightRequest { RootPath = _data }, _session);

        var json = Json(body);
        Assert.That(ctx.Response.StatusCode, Is.EqualTo(200));
        Assert.That(json.GetProperty("defaultFileName").GetString(), Is.EqualTo(CatalogNaming.DefaultFileName(_data)));
        Assert.That(json.GetProperty("catalogDir").GetString(), Is.EqualTo(Path.GetFullPath(_cat)).IgnoreCase);
        Assert.That(json.GetProperty("exists").GetBoolean(), Is.False);
        Assert.That(json.GetProperty("writable").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("rootAccessible").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("sameRootCatalogIds").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Streams_progress_then_done_and_adds_the_catalog()
    {
        var (ctx, body) = NewContext();

        await CreateCatalogEndpoint.HandleAsync(ctx, Req(), _session);

        var events = Events(Text(body));
        Assert.That(ctx.Response.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(events.Select(e => e.Event), Does.Contain("progress"));
        Assert.That(events.Last().Event, Is.EqualTo("done"));
        Assert.That(events.Count(e => e.Event is "done" or "error"), Is.EqualTo(1));
        var done = events.Last().Data;
        Assert.That(done.GetProperty("catalogId").GetInt32(), Is.EqualTo(1));
        Assert.That(done.GetProperty("entries").GetInt64(), Is.EqualTo(2));
        Assert.That(done.GetProperty("excludedFolderCount").GetInt32(), Is.Zero);
        Assert.That(done.GetProperty("accessDeniedFolderCount").GetInt32(), Is.Zero);
        Assert.That(done.GetProperty("unreadableFolderCount").GetInt32(), Is.Zero);
        Assert.That(done.GetProperty("savedPath").GetString(), Is.EqualTo(Path.Combine(Path.GetFullPath(_cat), "new.cdex")).IgnoreCase);
        Assert.That(events.First(e => e.Event == "progress").Data.GetProperty("phase").GetString(), Is.EqualTo("scanning"));
        Assert.That(_session.CatalogCount, Is.EqualTo(2));
        Assert.That(File.Exists(Path.Combine(_cat, "new.cdex")), Is.True);
    }

    [Test]
    public async Task An_invalid_file_name_is_400_before_the_stream()
    {
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req(@"a\b"), _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(400));
        Assert.That(ctx.Response.ContentType, Does.StartWith("application/json"));
        Assert.That(Json(body).GetProperty("code").GetString(), Is.EqualTo("invalid-file-name"));
        Assert.That(_session.CatalogCount, Is.EqualTo(1));
    }

    [Test]
    public async Task An_existing_loaded_file_is_409_with_the_existing_catalog_id()
    {
        var bytes = File.ReadAllBytes(_existing);
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req("existing"), _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(409));
        var json = Json(body);
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("file-exists"));
        Assert.That(json.GetProperty("existingCatalogId").GetInt32(), Is.EqualTo(0));
        Assert.That(File.ReadAllBytes(_existing), Is.EqualTo(bytes));
    }

    [Test]
    public async Task An_existing_file_that_is_not_loaded_is_409_without_an_existing_catalog_id()
    {
        File.WriteAllText(Path.Combine(_cat, "stray.cdex"), "x");
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req("stray"), _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(409));
        Assert.That(Json(body).GetProperty("code").GetString(), Is.EqualTo("file-exists"));
        Assert.That(Json(body).GetProperty("existingCatalogId").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task An_unreachable_root_is_422()
    {
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req(root: Path.Combine(_dir, "nope")), _session);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        var json = Json(body);
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("root-not-accessible"));
        Assert.That(json.GetProperty("rootPath").GetString(), Does.EndWith("nope"));
    }

    [Test]
    public async Task A_missing_working_folder_is_422_folder_not_writable()
    {
        using var missing = new CatalogSession(new DirLoader(), null);
        await missing.LoadAsync(Path.Combine(_dir, "gone")); // the folder doesn't exist
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req(), missing);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        Assert.That(Json(body).GetProperty("code").GetString(), Is.EqualTo("folder-not-writable"));
    }

    [Test]
    public async Task No_working_folder_yet_is_422()
    {
        using var fresh = new CatalogSession(new DirLoader(), null);
        var (ctx, body) = NewContext();
        await CreateCatalogEndpoint.HandleAsync(ctx, Req(), fresh);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(422));
        Assert.That(Json(body).GetProperty("code").GetString(), Is.EqualTo("no-catalog-dir"));
    }

    [Test]
    public void A_busy_session_is_409()
    {
        var refusal = CreateCatalogEndpoint.Refuse(new CreatePreflightResult { Busy = true }, Req());
        Assert.That(refusal!.Value.Status, Is.EqualTo(409));
        Assert.That(refusal.Value.Error.Code, Is.EqualTo("busy"));
    }

    [Test]
    public void A_clean_preflight_is_not_refused()
    {
        var pf = new CreatePreflightResult
        {
            CatalogDir = _cat,
            Writable = true,
            RootAccessible = true,
            FileName = "x",
            TargetPath = "x.cdex"
        };
        Assert.That(CreateCatalogEndpoint.Refuse(pf, Req()), Is.Null);
    }

    [Test]
    public async Task Client_abort_cancels_the_create_and_leaves_nothing_behind()
    {
        for (var i = 0; i < 1500; i++) File.WriteAllText(Path.Combine(_data, $"f{i}.txt"), "");
        var before = Directory.GetFiles(_cat);
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var (ctx, body) = NewContext(aborted.Token);

        await CreateCatalogEndpoint.HandleAsync(ctx, Req(), _session);

        Assert.That(Events(Text(body)).Select(e => e.Event), Does.Not.Contain("done"));
        Assert.That(Directory.GetFiles(_cat), Is.EquivalentTo(before));
        Assert.That(Directory.GetFiles(_cat, "*.tmp"), Is.Empty);
        Assert.That(_session.CatalogCount, Is.EqualTo(1));
        Assert.That(_session.AnyCatalogJobRunning, Is.False);
    }

    [Test]
    public void SseQueue_writes_create_progress_and_error_events()
    {
        var pipe = new TestPipeWriter();
        using var w = new SseWriter(pipe);
        SseQueue.WriteItem(w, new CreateProgress("scanning", 1000, @"C:\x"));
        SseQueue.WriteItem(w, new CreateCatalogError { Code = "file-exists", Message = "m", ExistingCatalogId = 3 });
        w.FlushAsync().AsTask().GetAwaiter().GetResult();
        Assert.That(pipe.Text, Is.EqualTo(
            "event: progress\ndata: {\"phase\":\"scanning\",\"entriesScanned\":1000,\"currentPath\":\"C:\\\\x\"}\n\n" +
            "event: error\ndata: {\"code\":\"file-exists\",\"message\":\"m\",\"rootPath\":null,\"existingCatalogId\":3}\n\n"));
    }
}
