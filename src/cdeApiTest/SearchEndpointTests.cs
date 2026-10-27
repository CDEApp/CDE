using System.Linq;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeAppCore.Validation;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives <see cref="SearchEndpoint"/> against a real search over a one-catalog session.</summary>
[TestFixture]
public class SearchEndpointTests
{
    private TestCatalog _catalog;

    [SetUp]
    public void SetUp() => _catalog = new TestCatalog();

    [TearDown]
    public void TearDown() => _catalog.Dispose();

    [Test]
    public async Task Invalid_query_is_400_with_the_validation_message_and_no_stream()
    {
        var query = new SearchQuery { RegexMode = true, Pattern = "(" };
        var expected = SearchFilterValidator.Validate(
            query.RegexMode, query.Pattern,
            query.FromSizeEnable, query.FromSize, query.ToSizeEnable, query.ToSize,
            query.FromDateEnable, query.FromDate, query.ToDateEnable, query.ToDate,
            query.FromHourEnable, query.FromHour, query.ToHourEnable, query.ToHour);
        Assert.That(expected.IsValid, Is.False, "test setup: the query must be invalid");
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(ctx, query, new SearchService(_catalog.Session));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(400));
        Assert.That(TestCatalog.Text(body), Is.EqualTo(expected.Message));
        Assert.That(ctx.Response.ContentType, Is.Not.EqualTo("text/event-stream"));
    }

    [Test]
    public async Task Valid_query_streams_each_match_then_done_with_the_count()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(ctx, new SearchQuery { Pattern = ".txt" }, new SearchService(_catalog.Session));

        var events = TestCatalog.Events(TestCatalog.Text(body));
        var results = events.Where(e => e.Event == "result").ToList();
        Assert.That(results.Select(r => r.Data.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "a.txt", "b.txt" }));
        Assert.That(events.Last().Event, Is.EqualTo("done"));
        Assert.That(events.Last().Data.GetProperty("count").GetInt32(), Is.EqualTo(2));
    }
}
