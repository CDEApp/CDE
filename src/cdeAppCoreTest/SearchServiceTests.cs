using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeLib;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class SearchServiceTests
{
    private CatalogFixture _fixture;
    private SearchService _search;

    [SetUp]
    public void SetUp()
    {
        _fixture = new CatalogFixture();
        _search = new SearchService(_fixture.Session);
    }

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    private static async Task<List<SearchResultRow>> Drain(
        IAsyncEnumerable<SearchResultRow> rows, CancellationToken ct = default)
    {
        var list = new List<SearchResultRow>();
        await foreach (var row in rows.WithCancellation(ct))
        {
            list.Add(row);
        }

        return list;
    }

    [Test]
    public async Task Search_StreamsMatchingRows()
    {
        var rows = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "alpha" }));

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt", "alpha.md" }));
    }

    [Test]
    public async Task Search_PopulatesRowFields()
    {
        var rows = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "alpha.md" }));

        Assert.That(rows, Has.Count.EqualTo(1));
        var row = rows[0];
        Assert.That(row.Name, Is.EqualTo("alpha.md"));
        Assert.That(row.FullPath, Is.EqualTo(@"C:\test\docs\alpha.md"));
        Assert.That(row.ParentPath, Is.EqualTo(@"C:\test\docs"));
        Assert.That(row.Size, Is.EqualTo(200));
        Assert.That(row.IsDirectory, Is.False);
        Assert.That(row.CatalogName, Is.EqualTo("test.cde"));
    }

    [Test]
    public async Task Search_HonoursResultLimit()
    {
        var rows = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "", LimitResultCount = 1 }));

        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Search_FiltersBySize()
    {
        var rows = await Drain(_search.SearchAsync(new SearchQuery
        {
            Pattern = "",
            IncludeFolders = false,
            FromSizeEnable = true,
            FromSize = 1000
        }));

        // Only beta.log (5000 bytes) exceeds the 1000-byte floor.
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Name, Is.EqualTo("beta.log"));
    }

    [Test]
    public async Task Search_PreCancelledToken_YieldsNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var rows = new List<SearchResultRow>();
        try
        {
            await foreach (var row in _search.SearchAsync(new SearchQuery { Pattern = "" }, null, cts.Token))
            {
                rows.Add(row);
            }
        }
        catch (OperationCanceledException)
        {
            // Acceptable: cancellation may surface as OCE rather than an empty stream.
        }

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public async Task Search_ReportsFinalProgress()
    {
        SearchProgress last = default;
        var seen = 0;
        var progress = new Progress<SearchProgress>(p => { last = p; Interlocked.Increment(ref seen); });

        // Progress<T> posts callbacks to the captured context; pump them by draining on the same flow.
        await Drain(_search.SearchAsync(new SearchQuery { Pattern = "alpha" }, progress));

        // Allow any queued Progress callbacks to run.
        await Task.Delay(50);

        Assert.That(seen, Is.GreaterThanOrEqualTo(1));
        Assert.That(last.Count, Is.EqualTo(last.Total));
        Assert.That(last.Total, Is.GreaterThan(0));
    }
}
