using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using cdeApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// Starts the real <c>Program</c> entry point in memory (<see cref="WebApplicationFactory{TEntryPoint}"/>) to check
/// the wiring: the route table, the token gate in front of it, CORS preflight and the startup catalog load. The
/// handshake token, the catalog folder and the settings stores are replaced, so the user's real
/// <c>%APPDATA%\cde</c> files are never touched. Not covered: Kestrel and the stdout handshake line.
/// </summary>
[TestFixture]
public class ProgramTests
{
    private const string Token = "test-token";

    private string _dir;
    private WebApplicationFactory<Program> _factory;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-api-program-test").FullName;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton(new HandshakeToken(Token));
            s.AddSingleton(new ApiOptions { ConfigPath = _dir });
            s.AddSingleton(new UiStateStore(Path.Combine(_dir, "ui-state.json")));
            s.AddSingleton(new ColourRulesStore(Path.Combine(_dir, "colour-rules.txt")));
        }));
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private HttpClient AuthorisedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TokenGate.HeaderName, Token);
        return client;
    }

    [Test]
    public void Every_route_is_mapped()
    {
        var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                .Select(m => $"{m} {e.RoutePattern.RawText}"))
            .Order()
            .ToList();

        Assert.That(routes, Is.EqualTo(new[]
        {
            "GET /catalogs",
            "GET /colour-rules",
            "GET /colour-rules/defaults",
            "GET /entries/{ref}/children",
            "GET /entries/{ref}/path",
            "GET /entries/{ref}/treemap",
            "GET /health",
            "GET /shell/commands",
            "GET /ui-state",
            "POST /catalogs",
            "POST /catalogs/preflight",
            "POST /catalogs/{id:int}/rescan",
            "POST /search",
            "POST /session/reload",
            "POST /shell",
            "PUT /colour-rules",
            "PUT /ui-state",
        }.Order()));
    }

    [Test]
    public async Task Request_without_the_token_is_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("unauthorized"));
    }

    [Test]
    public async Task Request_with_the_token_reaches_the_endpoint()
    {
        using var client = AuthorisedClient();

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("{\"status\":\"ok\"}"));
    }

    [Test]
    public async Task Bearer_token_reaches_the_endpoint()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Cors_preflight_is_answered_without_the_token()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "tauri://localhost");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "*" }));
    }

    [Test]
    public async Task Startup_loads_the_catalogs_in_the_configured_folder()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        TestCatalog.WriteCatalog(data, Path.Combine(_dir, "one.cdex"));
        using var client = AuthorisedClient();

        var catalogs = await client.GetFromJsonAsync<CatalogsResponse>("/catalogs", JsonSerializerOptions.Web);

        Assert.That(catalogs!.CatalogsLoaded, Is.EqualTo(1));
        Assert.That(catalogs.Catalogs.Single().RootPath, Does.StartWith(data));
    }

    [Test]
    public async Task Settings_endpoints_use_the_registered_stores()
    {
        using var client = AuthorisedClient();

        var response = await client.PutAsync("/ui-state", new StringContent("{\"catalogColumnCount\":7}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(File.ReadAllText(Path.Combine(_dir, "ui-state.json")), Does.Contain("7"));
    }
}
