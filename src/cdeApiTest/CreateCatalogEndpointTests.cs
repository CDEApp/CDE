using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class CreateCatalogEndpointTests
{
    private string _dir;
    private string _data;
    private string _cat;
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
        TestCatalog.WriteCatalog(_data, Path.Combine(_cat, "existing.cdex"));

        _session = new CatalogSession(new TestCatalog.FolderLoader(), null);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    [Test]
    public async Task Preflight_returns_the_dialog_facts_as_camel_case_json()
    {
        var (ctx, body) = TestCatalog.NewContext();
        await CreateCatalogEndpoint.PreflightAsync(ctx, new CreatePreflightRequest { RootPath = _data }, _session);

        var json = JsonDocument.Parse(TestCatalog.Text(body)).RootElement;
        Assert.That(ctx.Response.StatusCode, Is.EqualTo(200));
        Assert.That(json.GetProperty("defaultFileName").GetString(), Is.EqualTo(CatalogNaming.DefaultFileName(_data)));
        Assert.That(json.GetProperty("catalogDir").GetString(), Is.EqualTo(Path.GetFullPath(_cat)).IgnoreCase);
        Assert.That(json.GetProperty("exists").GetBoolean(), Is.False);
        Assert.That(json.GetProperty("writable").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("rootAccessible").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("sameRootCatalogIds").GetArrayLength(), Is.EqualTo(1));
    }
}
