using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeLib;
using cdeLib.Entities;
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
    public async Task Everything_mode_and_not_or_and_phrase_over_names()
    {
        var and = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "alp txt", EverythingMode = true }));
        Assert.That(and.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt" }));

        var not = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "alpha !md", EverythingMode = true }));
        Assert.That(not.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt" }));

        var or = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "<alpha.md | root_file> !docs", EverythingMode = true }));
        Assert.That(or.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.md", "root_file.txt" }));

        var phrase = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "\"alpha.txt\"", EverythingMode = true }));
        Assert.That(phrase.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt" }));
    }

    [Test]
    public async Task Everything_path_and_no_path_modifiers()
    {
        // 'docs' is only in the path of alpha.md; 'alpha' is in both alpha names.
        var viaPath = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "path:docs alpha", EverythingMode = true }));
        Assert.That(viaPath.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.md" }));

        var notPath = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "!path:docs alpha", EverythingMode = true }));
        Assert.That(notPath.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt" }));

        // With Include Path on, no-path: pulls one term back to the name: 'docs' is not in any name.
        var forced = await Drain(_search.SearchAsync(
            new SearchQuery { Pattern = "alpha no-path:docs", EverythingMode = true, IncludePath = true }));
        Assert.That(forced, Is.Empty);
    }

    [Test]
    public async Task Everything_mode_overrides_regex_mode_and_ignores_include_path()
    {
        // 'docs' only appears in a path, not in any name; with RegexMode also set it must still be a plain term.
        var byName = await Drain(_search.SearchAsync(
            new SearchQuery { Pattern = "docs alpha", EverythingMode = true, RegexMode = true }));
        Assert.That(byName, Is.Empty);

        // Include Path does not apply in Everything mode: only a path: term reaches the path.
        var ignored = await Drain(_search.SearchAsync(
            new SearchQuery { Pattern = "docs alpha", EverythingMode = true, RegexMode = true, IncludePath = true }));
        Assert.That(ignored, Is.Empty);

        var viaModifier = await Drain(_search.SearchAsync(
            new SearchQuery { Pattern = "path:docs alpha", EverythingMode = true, IncludePath = true }));
        Assert.That(viaModifier.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.md" }));
    }

    [Test]
    public async Task Plain_search_still_honours_include_path()
    {
        var byPath = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "docs", IncludePath = true }));
        Assert.That(byPath.ConvertAll(r => r.Name), Does.Contain("alpha.md"));
    }

    [Test]
    public async Task Empty_everything_query_matches_everything_and_other_filters_still_apply()
    {
        var all = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "  ", EverythingMode = true }));
        var plain = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "" }));
        Assert.That(all, Has.Count.EqualTo(plain.Count));

        var big = await Drain(_search.SearchAsync(new SearchQuery
        {
            Pattern = "alpha", EverythingMode = true, FromSizeEnable = true, FromSize = 150
        }));
        Assert.That(big.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.md" }));
    }

    [Test]
    public async Task Everything_mode_ignores_the_files_and_folders_flags_and_file_folder_filters_choose()
    {
        // The flags say "nothing", yet Everything mode searches both kinds and only the query text restricts.
        var folders = await Drain(_search.SearchAsync(new SearchQuery
            { Pattern = "folder:", EverythingMode = true, IncludeFiles = false, IncludeFolders = false }));
        Assert.That(folders.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "dir1", "docs" }));

        var files = await Drain(_search.SearchAsync(new SearchQuery
            { Pattern = "file: alpha", EverythingMode = true, IncludeFiles = false, IncludeFolders = false }));
        Assert.That(files.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "alpha.txt", "alpha.md" }));

        var both = await Drain(_search.SearchAsync(new SearchQuery
            { Pattern = "<folder: do> | <file: beta>", EverythingMode = true, IncludeFiles = false }));
        Assert.That(both.ConvertAll(r => r.Name), Is.EquivalentTo(new[] { "docs", "beta.log" }));
    }

    [Test]
    public async Task Plain_search_still_honours_the_files_and_folders_flags()
    {
        var onlyFiles = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "d", IncludeFolders = false }));
        Assert.That(onlyFiles.ConvertAll(r => r.IsDirectory), Has.None.True);
        var onlyFolders = await Drain(_search.SearchAsync(new SearchQuery { Pattern = "d", IncludeFiles = false }));
        Assert.That(onlyFolders.ConvertAll(r => r.IsDirectory), Has.All.True);
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

    private static RootEntry BigTree(int files)
    {
        var root = new RootEntry
        {
            Path = @"C:ig",
            VolumeName = "VOL",
            DefaultFileName = "big.cde",
            ActualFileName = "big.cde",
            DriveLetterHint = "C",
            Description = "d",
            AvailSpace = 1,
            TotalSpace = 2
        };
        for (var i = 0; i < files; i++) root.AddChild(new DirEntry(false) { Path = $"f{i}.txt", Size = i });
        root.SetInMemoryFields();
        return root;
    }

    [Test]
    public async Task Search_WithMoreMatchesThanTheBoundedQueue_DropsNothing()
    {
        using var fixture = new CatalogFixture(BigTree(5000));
        var search = new SearchService(fixture.Session);

        var rows = new List<SearchResultRow>();
        await foreach (var row in search.SearchAsync(new SearchQuery { Pattern = "", LimitResultCount = 100000 }))
        {
            if (rows.Count % 500 == 0) await Task.Delay(5); // slow consumer: the producer must wait, not drop
            rows.Add(row);
        }

        Assert.That(rows.FindAll(r => !r.IsDirectory), Has.Count.EqualTo(5000));
    }

    [Test]
    public async Task Search_AbandonedByTheConsumer_DoesNotLeaveTheProducerBlocked()
    {
        using var fixture = new CatalogFixture(BigTree(5000));
        var search = new SearchService(fixture.Session);

        var consume = Task.Run(async () =>
        {
            await foreach (var _ in search.SearchAsync(new SearchQuery { Pattern = "", LimitResultCount = 100000 }))
            {
                break; // leaves the bounded queue full
            }
        });

        Assert.That(await Task.WhenAny(consume, Task.Delay(TimeSpan.FromSeconds(10))), Is.SameAs(consume),
            "disposing the enumerator must release the producer");
    }
}
