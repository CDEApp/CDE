using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using cdeApi;
using cdeAppCore.Session;
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

builder.Services.AddCdeApi(logger);

var app = builder.Build();

// Must run before the token gate so CORS preflight (OPTIONS, sent without the token) is answered.
app.UseCors();

// --- handshake-token gate: nothing else on the box can call us without the token ---
app.UseTokenGate();

app.MapCdeApi();

// --- load the session up front, then start and report the handshake on stdout ---
var session = app.Services.GetRequiredService<ICatalogSession>();
try
{
    await session.LoadAsync(app.Services.GetRequiredService<ApiOptions>().ConfigPath);
    logger.Information("Loaded {Catalogs} catalogs ({Entries} entries)", session.CatalogCount, session.TotalEntryCount);
}
catch (Exception ex)
{
    logger.Warning(ex, "Initial catalog load failed; starting with an empty session");
}

await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:0";
var token = app.Services.GetRequiredService<HandshakeToken>().Value;

// Handshake line for the Tauri shell: exact, single line, flushed.
Console.WriteLine(JsonSerializer.Serialize(new { url = address, token }));
await Console.Out.FlushAsync();

await app.WaitForShutdownAsync();
return;

/// <summary>Public so tests can start the real entry point with <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
