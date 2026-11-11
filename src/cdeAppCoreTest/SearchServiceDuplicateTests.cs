using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

[TestFixture]
public class SearchServiceDuplicateTests
{
    private const string SameHex = "07070707070707070707070707070707";
    private const string UniqueHex = "08080808080808080808080808080808";

    private readonly List<string> _paths = [];
    private CatalogSession _session;
    private SearchService _search;

    [SetUp]
    public async Task SetUp()
    {
        foreach (var tree in new[]
                 {
                     Tree(@"C:\one", "ONE", ("same.bin", (byte?)7), ("unique.bin", 8), ("unhashed.bin", null)),
                     Tree(@"D:\two", "TWO", ("same.bin", 7), ("copy-of-same.bin", 7)),
                 })
        {
            var file = Path.Combine(Path.GetTempPath(), $"cdeappcoretest-dupecount-{Guid.NewGuid():N}.cdex");
            ColumnarFormat.Write(EntryStore.Build(tree), file);
            _paths.Add(file);
        }

        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(_paths);
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        await _session.LoadAsync("config");
        _search = new SearchService(_session);
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        foreach (var f in _paths) File.Delete(f);
    }

    private static RootEntry Tree(string path, string volume, params (string Name, byte? Hash)[] files)
    {
        var root = new RootEntry { Path = path, VolumeName = volume, DefaultFileName = volume + ".cde", DriveLetterHint = path[..1] };
        root.AddChild(new DirEntry(true) { Path = "folder" });
        foreach (var (name, hash) in files)
        {
            var file = new DirEntry(false) { Path = name, Size = 50 };
            if (hash is { } h) file.SetHash(new Hash16(Enumerable.Repeat(h, 16).ToArray()));
            root.AddChild(file);
        }

        root.SetInMemoryFields();
        return root;
    }

    private async Task<Dictionary<(int, string), SearchResultRow>> Rows(SearchQuery query)
    {
        var rows = new Dictionary<(int, string), SearchResultRow>();
        await foreach (var row in _search.SearchAsync(query)) rows.Add((row.Ref.CatalogId, row.Name), row);
        return rows;
    }

    [Test]
    public async Task Rows_carry_the_hash_and_count_the_other_copies_when_asked()
    {
        var rows = await Rows(new SearchQuery { Pattern = "", IncludeDuplicateCounts = true });

        Assert.That(rows[(0, "same.bin")].ContentHash, Is.EqualTo(SameHex));
        Assert.That(rows[(0, "same.bin")].DuplicateCount, Is.EqualTo(2));
        Assert.That(rows[(1, "copy-of-same.bin")].DuplicateCount, Is.EqualTo(2));
        Assert.That(rows[(0, "unique.bin")].ContentHash, Is.EqualTo(UniqueHex));
        Assert.That(rows[(0, "unique.bin")].DuplicateCount, Is.Zero);
        Assert.That(rows[(0, "unhashed.bin")].ContentHash, Is.Null);
        Assert.That(rows[(0, "unhashed.bin")].DuplicateCount, Is.Null);
        Assert.That(rows[(0, "folder")].ContentHash, Is.Null);
        Assert.That(rows[(0, "folder")].DuplicateCount, Is.Null);
    }

    [Test]
    public async Task Without_the_option_rows_still_carry_the_hash_but_no_count()
    {
        var rows = await Rows(new SearchQuery { Pattern = "", EverythingMode = true });

        Assert.That(rows[(0, "same.bin")].ContentHash, Is.EqualTo(SameHex));
        Assert.That(rows.Values.Select(r => r.DuplicateCount), Is.All.Null);
    }

    [Test]
    public async Task The_count_and_a_dupe_search_share_one_index()
    {
        var rows = await Rows(new SearchQuery { Pattern = "dupe:hash", EverythingMode = true, IncludeDuplicateCounts = true });

        Assert.That(rows.Keys, Is.EquivalentTo(new[] { (0, "same.bin"), (1, "same.bin"), (1, "copy-of-same.bin") }));
        Assert.That(rows.Values.Select(r => r.DuplicateCount), Is.All.EqualTo(2));
    }

    [Test]
    public async Task Hash_of_a_row_finds_that_file_and_its_copies_in_every_catalog()
    {
        var rows = await Rows(new SearchQuery { Pattern = "hash:" + SameHex, EverythingMode = true });

        Assert.That(rows.Keys, Is.EquivalentTo(new[] { (0, "same.bin"), (1, "same.bin"), (1, "copy-of-same.bin") }));
    }
}
