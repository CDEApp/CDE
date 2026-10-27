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
            "POST /catalogs/migrate",
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

    private static string WriteCatalogIn(string dir)
    {
        var data = Path.Combine(dir, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        TestCatalog.WriteCatalog(data, Path.Combine(dir, "one.cdex"));
        return data;
    }

    private static async Task<CatalogsResponse> CatalogsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CatalogsResponse>("/catalogs", JsonSerializerOptions.Web))!;

    // The handshake must not wait on catalogs (a folder of legacy .cde files took over a minute); the UI
    // starts the load with /session/reload and shows its progress.
    [Test]
    public async Task Startup_does_not_load_catalogs()
    {
        WriteCatalogIn(_dir);
        using var client = AuthorisedClient();

        Assert.That((await CatalogsAsync(client)).CatalogsLoaded, Is.EqualTo(0));
    }

    [Test]
    public async Task Reload_without_a_path_loads_the_configured_folder()
    {
        var data = WriteCatalogIn(_dir);
        using var client = AuthorisedClient();

        var reload = await client.PostAsync("/session/reload", null);

        var done = TestCatalog.Events(await reload.Content.ReadAsStringAsync()).Last();
        Assert.That(done.Event, Is.EqualTo("done"));
        Assert.That(done.Data.GetProperty("catalogs").GetInt32(), Is.EqualTo(1));
        var catalogs = await CatalogsAsync(client);
        Assert.That(catalogs.CatalogsLoaded, Is.EqualTo(1));
        Assert.That(catalogs.Catalogs.Single().RootPath, Does.StartWith(data));
    }

    // Legacy .cde catalogs are slow and memory-heavy to load, so the sidecar lists them for the UI to migrate.
    [Test]
    public async Task Reload_lists_legacy_catalogs_instead_of_loading_them()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        var re = new cdeLib.Entities.RootEntry();
        re.PopulateRoot(data);
        re.ActualFileName = Path.Combine(_dir, "old.cde");
        using (var repo = new cdeLib.Catalog.CatalogRepository(Serilog.Core.Logger.None)) await repo.Save(re);
        using var client = AuthorisedClient();

        await client.PostAsync("/session/reload", null);

        var catalogs = await CatalogsAsync(client);
        Assert.That(catalogs.CatalogsLoaded, Is.EqualTo(0));
        var legacy = catalogs.LegacyCatalogs.Single();
        Assert.That(legacy.Path, Is.EqualTo(re.ActualFileName));
        Assert.That(legacy.SizeBytes, Is.GreaterThan(0));
        Assert.That(legacy.Writable, Is.True);
    }

    // The sidecar's current directory is just wherever cde-tauri was launched, so it is not searched.
    [Test]
    public async Task Reload_does_not_search_the_current_directory()
    {
        var cwd = Directory.CreateTempSubdirectory("cde-api-program-cwd").FullName;
        var cwdBefore = Directory.GetCurrentDirectory();
        try
        {
            WriteCatalogIn(cwd);
            Directory.SetCurrentDirectory(cwd);
            using var client = AuthorisedClient();

            await client.PostAsync("/session/reload", null);

            Assert.That((await CatalogsAsync(client)).CatalogsLoaded, Is.EqualTo(0));
        }
        finally
        {
            Directory.SetCurrentDirectory(cwdBefore);
            Directory.Delete(cwd, recursive: true);
        }
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
