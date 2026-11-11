using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeAppCore.Session;
using cdeAppCore.Validation;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
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
    public async Task Malformed_everything_query_is_400_with_a_position()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "a || b", EverythingMode = true }, new SearchService(_catalog.Session));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(400));
        Assert.That(TestCatalog.Text(body), Does.Contain("position 3"));
    }

    [Test]
    public async Task Empty_regex_pattern_streams_the_same_results_as_an_empty_plain_pattern()
    {
        var plain = await ResultNames(new SearchQuery { Pattern = "" });

        var regex = await ResultNames(new SearchQuery { Pattern = "", RegexMode = true });

        Assert.That(regex, Is.Not.Empty.And.EquivalentTo(plain));
    }

    [Test]
    public async Task Everything_query_streams_matches()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "<a|b> txt !sub", EverythingMode = true },
            new SearchService(_catalog.Session));

        var results = TestCatalog.Events(TestCatalog.Text(body)).Where(e => e.Event == "result").ToList();
        Assert.That(results.Select(r => r.Data.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "a.txt", "b.txt" }));
    }

    [Test]
    public async Task Everything_folder_filter_streams_folders_whatever_the_flags_say()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "folder:", EverythingMode = true, IncludeFiles = false, IncludeFolders = false },
            new SearchService(_catalog.Session));

        var results = TestCatalog.Events(TestCatalog.Text(body)).Where(e => e.Event == "result").ToList();
        Assert.That(results.Select(r => r.Data.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "sub" }));
    }

    [Test]
    public async Task Everything_ignores_include_path_and_path_modifier_reaches_the_path()
    {
        // 'sub' is only in the path of b.txt.
        var (ctx, body) = TestCatalog.NewContext();
        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "sub txt", EverythingMode = true, IncludePath = true },
            new SearchService(_catalog.Session));
        Assert.That(TestCatalog.Events(TestCatalog.Text(body)).Where(e => e.Event == "result"), Is.Empty);

        var (ctx2, body2) = TestCatalog.NewContext();
        await SearchEndpoint.HandleAsync(
            ctx2, new SearchQuery { Pattern = "path:sub txt", EverythingMode = true, IncludePath = true },
            new SearchService(_catalog.Session));
        var results = TestCatalog.Events(TestCatalog.Text(body2)).Where(e => e.Event == "result").ToList();
        Assert.That(results.Select(r => r.Data.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "b.txt" }));
    }

    [Test]
    public async Task Path_modifier_in_front_of_a_filter_is_400_with_a_position()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "a path:file:", EverythingMode = true }, new SearchService(_catalog.Session));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(400));
        Assert.That(TestCatalog.Text(body), Does.Contain("position 2"));
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

    [Test]
    public async Task Results_carry_content_hash_and_duplicate_count_only_when_there_is_one()
    {
        var root = new RootEntry { Path = @"C:\data", VolumeName = "DATA", DefaultFileName = "DATA.cde", DriveLetterHint = "C" };
        foreach (var (name, hash) in new[] { ("a.bin", 7UL), ("b.bin", 7UL), ("c.bin", 8UL), ("plain.txt", 0UL) })
        {
            var file = new DirEntry(false) { Path = name, Size = 1 };
            if (hash != 0) file.SetHash(new Hash16(hash, hash));
            root.AddChild(file);
        }

        root.SetInMemoryFields();
        var cdex = Path.Combine(_catalog.Dir, "hashed.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), cdex);
        using var session = new CatalogSession(new TestCatalog.RecordingLoader(cdex), null);
        await session.LoadAsync("hashed");
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(ctx, new SearchQuery { Pattern = "", IncludeDuplicateCounts = true }, new SearchService(session));

        var rows = TestCatalog.Events(TestCatalog.Text(body)).Where(e => e.Event == "result")
            .ToDictionary(e => e.Data.GetProperty("name").GetString(), e => e.Data);
        Assert.That(rows["a.bin"].GetProperty("contentHash").GetString(), Is.EqualTo("00000000000000070000000000000007"));
        Assert.That(rows["a.bin"].GetProperty("duplicateCount").GetInt32(), Is.EqualTo(1));
        Assert.That(rows["c.bin"].GetProperty("duplicateCount").GetInt32(), Is.Zero);
        Assert.That(rows["plain.txt"].TryGetProperty("contentHash", out _), Is.False);
        Assert.That(rows["plain.txt"].TryGetProperty("duplicateCount", out _), Is.False);
    }

    [Test]
    public async Task Malformed_hash_is_400_with_a_position()
    {
        var (ctx, body) = TestCatalog.NewContext();

        await SearchEndpoint.HandleAsync(
            ctx, new SearchQuery { Pattern = "a hash:123", EverythingMode = true }, new SearchService(_catalog.Session));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(400));
        Assert.That(TestCatalog.Text(body), Does.Contain("32 hex digits").And.Contain("position 2"));
    }

    private async Task<string[]> ResultNames(SearchQuery query)
    {
        var (ctx, body) = TestCatalog.NewContext();
        await SearchEndpoint.HandleAsync(ctx, query, new SearchService(_catalog.Session));
        Assert.That(ctx.Response.StatusCode, Is.Not.EqualTo(400), TestCatalog.Text(body));
        return TestCatalog.Events(TestCatalog.Text(body))
            .Where(e => e.Event == "result").Select(r => r.Data.GetProperty("name").GetString()).ToArray();
    }
}
