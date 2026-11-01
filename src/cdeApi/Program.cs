using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using cdeApi;
using cdeLib.Infrastructure.Config;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;
using ILogger = Serilog.ILogger;

var builder = WebApplication.CreateBuilder(args);

// --- configuration: appsettings.json (found as cde finds it, and written as a default when there is none)
// + the shared user-scoped shell.json (custom commands), both in the config folder (#158) ---
var settingsLocator = new AppSettingsLocator();
var settingsInUse = ApiConfiguration.UseAppSettings(builder.Configuration, settingsLocator);
ApiConfiguration.AddShellJson(builder.Configuration, settingsLocator);

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
logger.Information("{ConfigurationInUse}", settingsInUse.Describe());

builder.Services.AddCdeApi(logger);

var app = builder.Build();

// Must run before the token gate so CORS preflight (OPTIONS, sent without the token) is answered.
app.UseCors();

// --- handshake-token gate: nothing else on the box can call us without the token ---
app.UseTokenGate();

app.MapCdeApi();

// --- start and report the handshake on stdout ---
// No catalogs are loaded here: the UI starts the load with POST /session/reload and shows its progress. Loading
// first would hold back the handshake, and a folder of legacy .cde catalogs takes longer than the UI waits.
await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:0";
var token = app.Services.GetRequiredService<HandshakeToken>().Value;

// Handshake line for the Tauri shell: exact, single line, flushed.
Console.WriteLine(JsonSerializer.Serialize(new { url = address, token }));
await Console.Out.FlushAsync();

await app.WaitForShutdownAsync();
return;
