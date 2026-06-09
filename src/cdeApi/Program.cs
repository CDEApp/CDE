using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using cdeAppCore.Shell;
using cdeAppCore.Validation;
using cdeLib;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;
using ILogger = Serilog.ILogger;

var builder = WebApplication.CreateBuilder(args);

// --- configuration: appsettings.json + the shared user-scoped shell.json (custom commands) ---
var shellJson = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cde", "shell.json");
builder.Configuration.AddJsonFile(shellJson, optional: true, reloadOnChange: false);

// Loopback only, ephemeral port. The real port is reported on stdout after start (handshake).
builder.WebHost.UseUrls("http://127.0.0.1:0");

// --- logging --- all logs go to stderr so stdout carries only the handshake line for the sidecar
// host. Clear ASP.NET's default stdout console provider for the same reason.
builder.Logging.ClearProviders();
ILogger logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
    .CreateLogger();
Log.Logger = logger;

// --- handshake token: nothing else on the box can call us without it ---
var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

// --- custom command list (config-sourced, injected; core never reads config itself) ---
var customCommands = builder.Configuration.GetSection("CustomCommands")
    .Get<List<CustomCommandOptions>>() ?? [];

var configPath = builder.Configuration["Cde:ConfigPath"] ?? ".";

// --- services ---
builder.Services.AddSingleton(logger);
builder.Services.AddSingleton<ILoadCatalogService>(_ => new LoadCatalogService(logger));
builder.Services.AddSingleton<ICatalogSession>(sp =>
    new CatalogSession(sp.GetRequiredService<ILoadCatalogService>(), logger));
builder.Services.AddSingleton<ISearchService>(sp =>
    new SearchService(sp.GetRequiredService<ICatalogSession>()));
builder.Services.AddSingleton<IShellActions>(_ => OperatingSystem.IsWindows()
    ? new WindowsShellActions(customCommands, logger)
    : new NoopShellActions(customCommands, logger));
builder.Services.AddSingleton<UiStateStore>();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppCoreJsonContext.Default));

// CORS: the webview runs on a different origin (http://localhost:1420 in dev, tauri://localhost in
// production) than this loopback sidecar. The handshake token — not the origin — is the security
// boundary, so any origin is allowed; the token middleware below still gates every real request.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Must run before the token gate so CORS preflight (OPTIONS, sent without the token) is answered.
app.UseCors();

// --- handshake-token gate: every request must present the token ---
app.Use(async (ctx, next) =>
{
    // CORS preflight is answered by UseCors above and carries no token; let it through.
    if (HttpMethods.IsOptions(ctx.Request.Method))
    {
        await next();
        return;
    }

    var provided = ctx.Request.Headers["X-CDE-Token"].FirstOrDefault();
    if (provided is null)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (auth is not null && auth.StartsWith("Bearer ", StringComparison.Ordinal))
            provided = auth["Bearer ".Length..];
    }

    if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(provided ?? ""),
            System.Text.Encoding.ASCII.GetBytes(token)))
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsync("unauthorized");
        return;
    }

    await next();
});

// --- endpoints ---
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/catalogs", (ICatalogSession session) =>
{
    using var proc = Process.GetCurrentProcess();
    return Results.Json(new CatalogsResponse
    {
        Catalogs = session.GetCatalogs(),
        CatalogsLoaded = session.CatalogCount,
        TotalEntries = session.TotalEntryCount,
        MemoryBytes = proc.PrivateMemorySize64
    });
});

app.MapGet("/entries/{ref}/children", (string @ref, bool? foldersOnly, int? skip, int? take,
    string sort, ICatalogSession session) =>
{
    if (!EntryRefRoute.TryParse(@ref, out var entryRef)) return Results.BadRequest("bad ref");
    var nodes = session.GetChildren(entryRef, foldersOnly ?? false, skip ?? 0, take ?? int.MaxValue);
    return Results.Json(DirectoryNodeSort.Apply(nodes, sort));
});

app.MapGet("/entries/{ref}/path", (string @ref, ICatalogSession session) =>
{
    if (!EntryRefRoute.TryParse(@ref, out var entryRef)) return Results.BadRequest("bad ref");
    return Results.Json(session.GetPath(entryRef));
});

// POST /shell — entry-ref guarded (D11): resolve the path from the session, re-check existence,
// then invoke the shell action in this (sidecar) process. Never accepts a client-supplied path.
app.MapPost("/shell", (ShellRequest req, ICatalogSession session, IShellActions shell) =>
{
    if (req is null || !EntryRefRoute.TryParse(req.Ref, out var entryRef))
        return Results.BadRequest("bad ref");
    if (!session.ExistsOnFileSystem(entryRef))
        return Results.BadRequest("entry not present on this filesystem");

    var path = session.ResolveFullPath(entryRef);
    switch (req.Action)
    {
        case "open": shell.Open(path); break;
        case "explore": shell.Explore(path); break;
        case "properties": shell.ShowProperties(path); break;
        case "custom": shell.RunCustomCommand(req.CommandId ?? -1, path); break;
        default: return Results.BadRequest("unknown action");
    }

    return Results.Ok();
});

// The configured custom commands a frontend renders as menu items; /shell runs one by its id (D10).
app.MapGet("/shell/commands", (IShellActions shell) =>
    Results.Json(shell.CustomCommands.Select((c, i) => new { id = i, label = c.Label }).ToList()));

app.MapGet("/ui-state", (UiStateStore store) => Results.Content(store.GetMergedJson(), "application/json"));

app.MapPut("/ui-state", async (HttpContext ctx, UiStateStore store) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    try
    {
        store.Save(body);
        return Results.Ok();
    }
    catch (JsonException)
    {
        return Results.BadRequest("body is not valid JSON");
    }
});

// POST /session/reload — reload catalogs, streaming load progress over SSE then a final `done`.
// Optional ?path=<dir> loads .cdex catalogs from that folder (and one level down); otherwise the
// configured default path is used.
app.MapPost("/session/reload", async (HttpContext ctx, ICatalogSession session) =>
{
    var requested = ctx.Request.Query["path"].FirstOrDefault();
    var loadPath = string.IsNullOrWhiteSpace(requested) ? configPath : requested;

    var ct = ctx.RequestAborted;
    Sse.Start(ctx.Response);

    var channel = Channel.CreateUnbounded<(string evt, string data)>();
    var progress = new SseProgress<CatalogLoadProgress>(channel.Writer,
        p => JsonSerializer.Serialize(p, AppCoreJsonContext.Default.CatalogLoadProgress));

    var work = Task.Run(async () =>
    {
        try { await session.LoadAsync(loadPath, progress, ct); }
        finally
        {
            channel.Writer.TryWrite(("done",
                JsonSerializer.Serialize(new { catalogs = session.CatalogCount, entries = session.TotalEntryCount })));
            channel.Writer.Complete();
        }
    }, CancellationToken.None);

    await Drain(ctx.Response, channel.Reader, ct);
    await work;
});

// POST /search — validate first (400 + message), then stream result/progress/done over SSE.
// Cancels on client disconnect via RequestAborted.
app.MapPost("/search", async (HttpContext ctx, SearchQuery query, ISearchService search) =>
{
    var v = SearchFilterValidator.Validate(
        query.RegexMode, query.Pattern,
        query.FromSizeEnable, query.FromSize, query.ToSizeEnable, query.ToSize,
        query.FromDateEnable, query.FromDate, query.ToDateEnable, query.ToDate,
        query.FromHourEnable, query.FromHour, query.ToHourEnable, query.ToHour);
    if (!v.IsValid)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync(v.Message);
        return;
    }

    var ct = ctx.RequestAborted;
    Sse.Start(ctx.Response);

    var channel = Channel.CreateUnbounded<(string evt, string data)>();
    var progress = new SseProgress<SearchProgress>(channel.Writer,
        p => JsonSerializer.Serialize(p, AppCoreJsonContext.Default.SearchProgress));

    var work = Task.Run(async () =>
    {
        var count = 0;
        try
        {
            await foreach (var row in search.SearchAsync(query, progress, ct))
            {
                count++;
                channel.Writer.TryWrite(("result",
                    JsonSerializer.Serialize(row, AppCoreJsonContext.Default.SearchResultRow)));
            }
        }
        catch (OperationCanceledException) { /* client disconnect / cancel */ }
        finally
        {
            channel.Writer.TryWrite(("done", JsonSerializer.Serialize(new { count })));
            channel.Writer.Complete();
        }
    }, CancellationToken.None);

    await Drain(ctx.Response, channel.Reader, ct);
    await work;
});

// --- load the session up front, then start and report the handshake on stdout ---
var session = app.Services.GetRequiredService<ICatalogSession>();
try
{
    await session.LoadAsync(configPath);
    logger.Information("Loaded {Catalogs} catalogs ({Entries} entries)", session.CatalogCount, session.TotalEntryCount);
}
catch (Exception ex)
{
    logger.Warning(ex, "Initial catalog load failed; starting with an empty session");
}

await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:0";

// Handshake line for the Tauri shell: exact, single line, flushed.
Console.WriteLine(JsonSerializer.Serialize(new { url = address, token }));
Console.Out.Flush();

await app.WaitForShutdownAsync();
return;

// Single-threaded drain of the SSE event channel to the response body.
static async Task Drain(HttpResponse response, ChannelReader<(string evt, string data)> reader, CancellationToken ct)
{
    try
    {
        await foreach (var (evt, data) in reader.ReadAllAsync(ct))
        {
            await Sse.EventAsync(response, evt, data, ct);
        }
    }
    catch (OperationCanceledException) { /* client disconnected */ }
}

/// <summary>Bridges an <see cref="IProgress{T}"/> report into an SSE <c>progress</c> event on the channel.</summary>
internal sealed class SseProgress<T>(ChannelWriter<(string evt, string data)> writer, Func<T, string> serialize)
    : IProgress<T>
{
    public void Report(T value) => writer.TryWrite(("progress", serialize(value)));
}
