using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeAppCore.Treemap;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// Drives the <c>/entries/{ref}/...</c> handlers in <see cref="EntryEndpoints"/> against a real
/// <see cref="CatalogSession"/> over a temp folder, asserting on the returned <see cref="IResult"/>s. Not covered
/// here: Kestrel routing / query binding and JSON serialization.
/// </summary>
[TestFixture]
public class EntryEndpointsTests
{
    private string _dir;
    private CatalogSession _session;

    // data/
    //   a.txt (1 byte)
    //   b.txt (3 bytes)
    //   sub/
    //     inner/
    //       c.txt
    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-api-entries-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "sub", "inner"));
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        File.WriteAllText(Path.Combine(data, "b.txt"), "bbb");
        File.WriteAllText(Path.Combine(data, "sub", "inner", "c.txt"), "c");

        var re = new RootEntry();
        re.PopulateRoot(data);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        var cdex = Path.Combine(_dir, "one.cdex");
        re.ActualFileName = cdex;
        ColumnarFormat.Write(re, cdex);

        _session = new CatalogSession(new FixedLoader(cdex), null);
        _session.LoadAsync("x").GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    private static T Value<T>(IResult result) => (T)((IValueHttpResult)result).Value;

    private static int? Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;

    private DirectoryNodeDto Sub() =>
        Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", null, null, null, null, null, _session)).Single(n => n.Name == "sub");

    // ---- children ----

    [TestCase("")]
    [TestCase("nope")]
    [TestCase("0-")]
    public void Children_BadRef_IsBadRequest(string @ref)
    {
        Assert.That(Status(EntryEndpoints.Children(@ref, null, null, null, null, null, _session)), Is.EqualTo(400));
    }

    [Test]
    public void Children_Defaults_AllEntries_InCatalogOrder_WithHasChildren()
    {
        var nodes = Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", null, null, null, null, null, _session));

        Assert.That(nodes.Select(n => n.Name), Is.EquivalentTo(new[] { "a.txt", "b.txt", "sub" }));
        Assert.That(nodes.Single(n => n.Name == "sub").HasChildren, Is.True); // holds the 'inner' folder
    }

    [Test]
    public void Children_HasChildrenFalse_SkipsTheCheck()
    {
        var nodes = Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", null, null, null, null, false, _session));

        Assert.That(nodes.Single(n => n.Name == "sub").HasChildren, Is.False);
    }

    [Test]
    public void Children_FoldersOnly_Sorted_Paged()
    {
        var folders = Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", true, null, null, "name", true, _session));
        Assert.That(folders.Select(n => n.Name), Is.EqualTo(new[] { "sub" }));

        // Descending reverses the whole (directories-first, by size) order; skip/take page before sorting.
        var page = Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", false, 0, 3, "-size", null, _session));
        Assert.That(page.Select(n => n.Name), Is.EqualTo(new[] { "b.txt", "a.txt", "sub" }));

        var none = Value<IReadOnlyList<DirectoryNodeDto>>(
            EntryEndpoints.Children("0-0", false, 3, 10, null, null, _session));
        Assert.That(none, Is.Empty);
    }

    // ---- path ----

    [Test]
    public void Path_BadRef_IsBadRequest()
    {
        Assert.That(Status(EntryEndpoints.Path("x", _session)), Is.EqualTo(400));
    }

    [Test]
    public void Path_IsRootFirstChain()
    {
        var chain = Value<IReadOnlyList<DirectoryNodeDto>>(EntryEndpoints.Path(RefOf(Sub()), _session));

        Assert.That(chain, Has.Count.EqualTo(2));
        Assert.That(chain[0].Ref, Is.EqualTo(new EntryRefDto(0, 0)));
        Assert.That(chain[1].Name, Is.EqualTo("sub"));
    }

    // ---- treemap ----

    [Test]
    public void Treemap_BadRef_IsBadRequest()
    {
        Assert.That(Status(EntryEndpoints.Treemap("x", null, null, _session, new TreemapService(_session))),
            Is.EqualTo(400));
    }

    [TestCase("1-0")]        // no such catalog
    [TestCase("0-1000000")]  // entry index past the catalog
    public void Treemap_UnknownEntry_IsNotFound(string @ref)
    {
        Assert.That(Status(EntryEndpoints.Treemap(@ref, null, null, _session, new TreemapService(_session))),
            Is.EqualTo(404));
    }

    [Test]
    public void Treemap_DefaultsAndExplicitArguments_BuildForTheEntry()
    {
        var treemap = new TreemapService(_session);

        var deep = Value<TreemapResponse>(EntryEndpoints.Treemap("0-0", null, null, _session, treemap));
        var shallow = Value<TreemapResponse>(EntryEndpoints.Treemap("0-0", 1, 0.0, _session, treemap));

        // Default depth (3) reaches c.txt under sub/inner; depth 1 stops at the root's own children.
        Assert.That(deep.Rows.Select(r => r.Name), Does.Contain("c.txt"));
        Assert.That(shallow.Rows.Select(r => r.Name), Does.Not.Contain("c.txt"));
        Assert.That(shallow.Rows.Select(r => r.Name), Does.Contain("sub"));
    }

    // ---- routes over HTTP (loopback Kestrel): templates and query-string binding by name ----

    [Test]
    public async Task MapEntryEndpoints_RoutesAndBindsQueryParameters()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ICatalogSession>(_session);
        builder.Services.AddSingleton<ITreemapService>(new TreemapService(_session));
        await using var app = builder.Build();
        app.MapEntryEndpoints();
        await app.StartAsync();

        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        async Task<T> Get<T>(string url) =>
            (await http.GetFromJsonAsync<T>(url, JsonSerializerOptions.Web))!;

        var withCheck = await Get<List<DirectoryNodeDto>>("/entries/0-0/children?foldersOnly=true&sort=name");
        var without = await Get<List<DirectoryNodeDto>>("/entries/0-0/children?hasChildren=false&skip=0&take=10");
        Assert.That(withCheck.Select(n => (n.Name, n.HasChildren)), Is.EqualTo(new[] { ("sub", true) }));
        Assert.That(without.Single(n => n.Name == "sub").HasChildren, Is.False);

        var chain = await Get<List<DirectoryNodeDto>>($"/entries/{RefOf(withCheck[0])}/path");
        Assert.That(chain.Select(n => n.Name).Last(), Is.EqualTo("sub"));

        var treemap = await Get<TreemapResponse>("/entries/0-0/treemap?depth=1&minFraction=0");
        Assert.That(treemap.Rows.Select(r => r.Name), Does.Contain("sub").And.Not.Contain("c.txt"));

        Assert.That((await http.GetAsync("/entries/bad/children")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        await app.StopAsync();
    }

    private static string RefOf(DirectoryNodeDto node) => $"{node.Ref.CatalogId}-{node.Ref.EntryIndex}";

    private sealed class FixedLoader(string cdex) : ILoadCatalogService
    {
        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath) => [cdex];
    }
}
