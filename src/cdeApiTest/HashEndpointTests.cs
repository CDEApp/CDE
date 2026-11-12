using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class HashEndpointTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().AddLogging().BuildServiceProvider();

    private string _dir;
    private string _cat;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-api-hash-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        Directory.CreateDirectory(_cat);
        WriteCatalog("a", ("big.bin", 4096), ("small.txt", 10));
        WriteCatalog("b", ("big.bin", 4096), ("small.txt", 10), ("lonely.bin", 9000));

        _session = new CatalogSession(new TestCatalog.FolderLoader(), null);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { }
    }

    private void WriteCatalog(string name, params (string File, int Size)[] files)
    {
        var data = Path.Combine(_dir, "data-" + name);
        Directory.CreateDirectory(data);
        foreach (var (file, size) in files) File.WriteAllBytes(Path.Combine(data, file), new byte[size]);
        TestCatalog.WriteCatalog(data, Path.Combine(_cat, name + ".cdex"));
    }

    private async Task<(int Status, JsonElement Body)> Preflight(HashPreflightRequest request)
    {
        var (ctx, body) = TestCatalog.NewContext();
        ctx.RequestServices = Services;
        var result = HashEndpoint.Preflight(request, _session, ctx);
        await result.ExecuteAsync(ctx);
        return (ctx.Response.StatusCode, JsonDocument.Parse(TestCatalog.Text(body)).RootElement.Clone());
    }

    private static HashPreflightRequest Request(int[] ids, string filter = null, bool includeUniqueSizes = false) =>
        new() { CatalogIds = ids, Hash = new HashOptions { Filter = filter, IncludeUniqueSizes = includeUniqueSizes } };

    [Test]
    public async Task Preflight_counts_the_files_of_the_catalog_set_as_camel_case_json()
    {
        var (status, all) = await Preflight(new HashPreflightRequest { CatalogIds = [0, 1] });
        var (_, filtered) = await Preflight(Request([0, 1], "size:>1kb"));
        var (_, unique) = await Preflight(Request([0, 1], "size:>1kb", includeUniqueSizes: true));
        var (_, alone) = await Preflight(Request([0]));

        Assert.That(status, Is.EqualTo(200));
        Assert.That(all.GetRawText(), Is.EqualTo("""{"files":4,"bytes":8212,"alreadyHashed":0}"""));
        Assert.That(filtered.GetRawText(), Is.EqualTo("""{"files":2,"bytes":8192,"alreadyHashed":0}"""));
        Assert.That(unique.GetRawText(), Is.EqualTo("""{"files":3,"bytes":17192,"alreadyHashed":0}"""));
        Assert.That(alone.GetProperty("files").GetInt64(), Is.EqualTo(0), "no size repeats inside catalog 0");
    }

    [Test]
    public async Task Preflight_refuses_an_invalid_filter_as_400_with_the_search_bars_message()
    {
        var parserMessage = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("size:>>")).Message;

        var (status, error) = await Preflight(Request([0], "size:>>"));

        Assert.That(status, Is.EqualTo(400));
        Assert.That(error.GetProperty("code").GetString(), Is.EqualTo(JobErrorCodes.InvalidFilter));
        Assert.That(error.GetProperty("message").GetString(), Is.EqualTo(parserMessage));
    }

    [Test]
    public async Task Preflight_refuses_a_missing_body_no_catalog_and_an_unknown_catalog()
    {
        Assert.That((await Preflight(null)).Status, Is.EqualTo(400));
        var (emptyStatus, empty) = await Preflight(Request([]));
        var (unknownStatus, unknown) = await Preflight(Request([0, 4]));

        Assert.That((emptyStatus, empty.GetProperty("code").GetString()), Is.EqualTo((400, JobErrorCodes.InvalidRequest)));
        Assert.That((unknownStatus, unknown.GetProperty("code").GetString()), Is.EqualTo((404, JobErrorCodes.UnknownCatalog)));
    }

    [Test]
    public async Task Preflight_after_a_hash_job_reports_the_files_as_already_hashed()
    {
        await _session.HashAsync([0, 1], null);

        var (_, after) = await Preflight(Request([0, 1]));

        Assert.That(after.GetRawText(), Is.EqualTo("""{"files":0,"bytes":0,"alreadyHashed":4}"""));
    }
}
