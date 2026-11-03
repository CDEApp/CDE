using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Compare;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// <see cref="CompareEndpoint"/> over the <see cref="TestCatalog"/> (data/a.txt, data/sub/b.txt). Subtree
/// semantics are covered in cdeAppCoreTest; here: request parsing, status codes and the wire shape.
/// </summary>
[TestFixture]
public class CompareEndpointTests
{
    private TestCatalog _catalog;

    [SetUp]
    public void SetUp() => _catalog = new TestCatalog();

    [TearDown]
    public void TearDown() => _catalog.Dispose();

    private static IResult Post(ICompareService compare, params (string Left, string Right)[] pairs) =>
        CompareEndpoint.Handle(
            new CompareSummariesRequest { Pairs = pairs.Select(p => new ComparePairDto { Left = p.Left, Right = p.Right }).ToList() },
            compare, CancellationToken.None);

    private IResult Post(params (string Left, string Right)[] pairs) => Post(new CompareService(_catalog.Session), pairs);

    // Results.Json leaves the status unset, which means 200.
    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 200;

    private static CompareSummariesResponse Body(IResult result) =>
        (CompareSummariesResponse)((IValueHttpResult)result).Value;

    private string RefOf(string name)
    {
        var node = _catalog.Session.GetChildren(new EntryRefDto(0, 0)).Single(n => n.Name == name);
        return $"{node.Ref.CatalogId}-{node.Ref.EntryIndex}";
    }

    [Test]
    public void RootAgainstItself_IsAllSame_InPairOrder()
    {
        var result = Post(("0-0", "0-0"), (RefOf("sub"), RefOf("sub")));

        Assert.That(Status(result), Is.EqualTo(200));
        var s = Body(result).Summaries;
        Assert.That(s, Has.Count.EqualTo(2));
        Assert.That((s[0].SameFiles, s[0].DifferentFiles, s[0].LeftOnly, s[0].RightOnly), Is.EqualTo((2, 0, 0, 0)));
        Assert.That((s[1].SameFiles, s[1].DifferentFiles), Is.EqualTo((1, 0)));
        Assert.That((s[0].SameFolders, s[0].DifferentFolders), Is.EqualTo((1, 0)));
        Assert.That((s[1].SameFolders, s[1].DifferentFolders), Is.EqualTo((0, 0)));
    }

    [Test]
    public void NoPairs_IsAnEmptyList()
    {
        Assert.That(Body(Post()).Summaries, Is.Empty);
    }

    [Test]
    public void MissingPairs_IsBadRequest()
    {
        Assert.That(Status(CompareEndpoint.Handle(new CompareSummariesRequest(), new CompareService(_catalog.Session),
            CancellationToken.None)), Is.EqualTo(400));
        Assert.That(Status(CompareEndpoint.Handle(null, new CompareService(_catalog.Session), CancellationToken.None)),
            Is.EqualTo(400));
    }

    [TestCase("", "0-0")]
    [TestCase("0-0", "nope")]
    [TestCase("0-", "0-0")]
    [TestCase("0-0", "-1-0")]
    public void MalformedRef_IsBadRequest(string left, string right)
    {
        Assert.That(Status(Post((left, right))), Is.EqualTo(400));
    }

    [Test]
    public void NullPairOrNullRef_IsBadRequest()
    {
        var withNull = new CompareSummariesRequest { Pairs = [null] };
        Assert.That(Status(CompareEndpoint.Handle(withNull, new CompareService(_catalog.Session), CancellationToken.None)),
            Is.EqualTo(400));
        Assert.That(Status(Post((null, "0-0"))), Is.EqualTo(400));
    }

    [Test]
    public void NonDirectory_IsBadRequest()
    {
        Assert.That(Status(Post((RefOf("a.txt"), "0-0"))), Is.EqualTo(400));
    }

    [Test]
    public void EntryPastTheCatalog_IsBadRequest()
    {
        Assert.That(Status(Post(("0-0", "0-1000000"))), Is.EqualTo(400));
    }

    [Test]
    public void UnknownCatalog_IsNotFound()
    {
        Assert.That(Status(Post(("0-0", "5-0"))), Is.EqualTo(404));
    }

    [Test]
    public void MoreThan500Pairs_IsBadRequest_And500IsAccepted()
    {
        Assert.That(Status(Post(Enumerable.Repeat(("0-0", "0-0"), 501).ToArray())), Is.EqualTo(400));
        Assert.That(Status(Post(Enumerable.Repeat(("0-0", "0-0"), 500).ToArray())), Is.EqualTo(200));
    }

    [Test]
    public void ClientAbort_IsAnAbortedStatus_NotAnError()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var request = new CompareSummariesRequest { Pairs = [new ComparePairDto { Left = "0-0", Right = "0-0" }] };

        var result = CompareEndpoint.Handle(request, new CompareService(_catalog.Session), cts.Token);

        Assert.That(Status(result), Is.EqualTo(CompareEndpoint.ClientClosedRequest));
    }

    [Test]
    public async Task OverHttp_BodyBindsAndResponseIsCamelCase()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppCoreJsonContext.Default));
        builder.Services.AddSingleton<ICompareService>(new CompareService(_catalog.Session));
        await using var app = builder.Build();
        app.MapPost("/compare/summaries", CompareEndpoint.Handle);
        await app.StartAsync();

        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        using var ok = await http.PostAsync("/compare/summaries", Json("""{"pairs":[{"left":"0-0","right":"0-0"}]}"""));
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ok.Content.ReadAsStringAsync(),
            Is.EqualTo("""{"summaries":[{"sameFiles":2,"differentFiles":0,"leftNewerFiles":0,"rightNewerFiles":0,"leftOnly":0,"rightOnly":0,"sameFolders":1,"differentFolders":0,"leftNewerFolders":0,"rightNewerFolders":0}]}"""));

        using var missing = await http.PostAsync("/compare/summaries", Json("{}"));
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var unknown = await http.PostAsync("/compare/summaries", Json("""{"pairs":[{"left":"0-0","right":"9-0"}]}"""));
        Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");
}
