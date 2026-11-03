using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// ext:, empty: and the count functions over the mmap <see cref="ColumnarCatalogReader"/> and the in-memory
/// <see cref="EntryStore"/> must give the same rows as an oracle that works from the path strings alone.
/// </summary>
[TestFixture]
public class EverythingFolderParityTests
{
    private static RootEntry BuildTree()
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "VOL", DefaultFileName = "t.cde", DriveLetterHint = "C" };

        var docs = new DirEntry(true) { Path = "docs" };
        docs.AddChild(new DirEntry(false) { Path = "readme.md", Size = 10 });
        var img = new DirEntry(true) { Path = "img" };
        img.AddChild(new DirEntry(false) { Path = "a.png", Size = 20 });
        img.AddChild(new DirEntry(false) { Path = "b.JPG", Size = 30 });
        var raw = new DirEntry(true) { Path = "raw" };
        raw.AddChild(new DirEntry(false) { Path = "c.RAW", Size = 40 });
        img.AddChild(raw);
        docs.AddChild(img);
        docs.AddChild(new DirEntry(true) { Path = "empty" });
        root.AddChild(docs);

        var music = new DirEntry(true) { Path = "music" };
        music.AddChild(new DirEntry(false) { Path = "x.mp3", Size = 5 });
        music.AddChild(new DirEntry(false) { Path = "y.FLAC", Size = 6 });
        music.AddChild(new DirEntry(false) { Path = "z.mp3.bak", Size = 7 });
        music.AddChild(new DirEntry(false) { Path = ".hidden", Size = 8 });
        music.AddChild(new DirEntry(false) { Path = "noext", Size = 9 });
        music.AddChild(new DirEntry(false) { Path = "caf\u00e9.MP3", Size = 9 });
        root.AddChild(music);

        root.AddChild(new DirEntry(true) { Path = "void" });
        root.AddChild(new DirEntry(false) { Path = "top.txt", Size = 1 });
        root.AddChild(new DirEntry(false) { Path = "Photo.JPG", Size = 2 });
        root.SetInMemoryFields();
        return root;
    }

    public sealed record Row(string Name, string Path, bool IsFolder, int ChildFiles, int ChildFolders, int DescFiles, int DescFolders);

    // Everything the oracle knows comes from the path strings and the Directory flag, not from the tree links.
    private static List<Row> Rows(EntryStore store)
    {
        var paths = Enumerable.Range(0, store.Count).Select(i => store.FullPath(i)).ToList();
        var folder = Enumerable.Range(0, store.Count).Select(store.IsDirectory).ToList();
        var rows = new List<Row>();
        for (var i = 1; i < store.Count; i++)
        {
            var below = Enumerable.Range(1, store.Count - 1)
                .Where(j => paths[j].StartsWith(paths[i] + "\\", StringComparison.Ordinal)).ToList();
            var children = below.Where(j => paths[j].IndexOf('\\', paths[i].Length + 1) < 0).ToList();
            rows.Add(new Row(store.FullName(i), paths[i], folder[i],
                children.Count(j => !folder[j]), children.Count(j => folder[j]),
                below.Count(j => !folder[j]), below.Count(j => folder[j])));
        }

        return rows;
    }

    private static string ExtOf(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : string.Empty;
    }

    private static bool ExtIn(Row r, params string[] exts) =>
        !r.IsFolder && exts.Contains(ExtOf(r.Name), StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<TestCaseData> Cases()
    {
        yield return new TestCaseData("empty:", (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles + r.ChildFolders == 0));
        yield return new TestCaseData("!empty: folder:", (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles + r.ChildFolders > 0));
        yield return new TestCaseData("child-count:2", (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles + r.ChildFolders == 2));
        yield return new TestCaseData("child-count:>=2", (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles + r.ChildFolders >= 2));
        yield return new TestCaseData("child-count:1..3", (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles + r.ChildFolders is >= 1 and <= 3));
        yield return new TestCaseData("child-file-count:1 child-folder-count:0",
            (Func<Row, bool>)(r => r.IsFolder && r.ChildFiles == 1 && r.ChildFolders == 0));
        yield return new TestCaseData("child-folder-count:>0", (Func<Row, bool>)(r => r.IsFolder && r.ChildFolders > 0));
        yield return new TestCaseData("descendant-count:>3", (Func<Row, bool>)(r => r.IsFolder && r.DescFiles + r.DescFolders > 3));
        yield return new TestCaseData("descendant-file-count:2..4", (Func<Row, bool>)(r => r.IsFolder && r.DescFiles is >= 2 and <= 4));
        yield return new TestCaseData("descendant-folder-count:0 folder:", (Func<Row, bool>)(r => r.IsFolder && r.DescFolders == 0));
        yield return new TestCaseData("descendant-count:5 | empty:",
            (Func<Row, bool>)(r => r.IsFolder && (r.DescFiles + r.DescFolders == 5 || r.ChildFiles + r.ChildFolders == 0)));
        yield return new TestCaseData("ext:mp3;flac", (Func<Row, bool>)(r => ExtIn(r, "mp3", "flac")));
        yield return new TestCaseData("ext:", (Func<Row, bool>)(r => !r.IsFolder && ExtOf(r.Name).Length == 0));
        yield return new TestCaseData("ext:j*", (Func<Row, bool>)(r => !r.IsFolder && ExtOf(r.Name).StartsWith("j", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("case:ext:JPG", (Func<Row, bool>)(r => !r.IsFolder && ExtOf(r.Name) == "JPG"));
        yield return new TestCaseData("ext:mp3 | folder:", (Func<Row, bool>)(r => r.IsFolder || ExtIn(r, "mp3")));
        yield return new TestCaseData("ext:*.mp3;*.ba?", (Func<Row, bool>)(r => ExtIn(r, "mp3", "bak")));
        yield return new TestCaseData("ext:png path:docs", (Func<Row, bool>)(r => ExtIn(r, "png") && r.Path.Contains("docs", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("ext:caf\u00e9 | ext:mp3", (Func<Row, bool>)(r => ExtIn(r, "mp3")));
        yield return new TestCaseData("!ext:mp3 file:", (Func<Row, bool>)(r => !r.IsFolder && !ExtIn(r, "mp3")));
        yield return new TestCaseData("ext:jpg;raw size:>2", (Func<Row, bool>)(r => ExtIn(r, "jpg", "raw") && r.Name != "Photo.JPG"));
        yield return new TestCaseData("folder: !empty: ext:", (Func<Row, bool>)(_ => false));
    }

    [TestCaseSource(nameof(Cases))]
    public void ReaderAndStore_MatchTheOracle(string query, Func<Row, bool> oracle)
    {
        var store = EntryStore.Build(BuildTree());
        var rows = Rows(store);
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-evfold-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var expected = new List<int>();
            for (var i = 1; i < store.Count; i++)
            {
                if (oracle(rows[i - 1])) expected.Add(i);
            }

            var options = new EntryStoreFindOptions { Pattern = query, EverythingMode = true };
            var fromReader = new List<int>();
            reader.Find(options, fromReader.Add);
            var fromStore = new List<int>();
            EntryStoreSearch.Find(store, options, fromStore.Add);

            Assert.That(fromReader, Is.EqualTo(expected), $"reader '{query}'");
            Assert.That(fromStore, Is.EqualTo(expected), $"store '{query}'");
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void Oracle_sanity_the_fixture_has_the_shapes_the_cases_rely_on()
    {
        var rows = Rows(EntryStore.Build(BuildTree()));
        Assert.That(rows.Count(r => r.IsFolder && r.ChildFiles + r.ChildFolders == 0), Is.EqualTo(2)); // docs/empty and void
        Assert.That(rows.Single(r => r.Name == "docs").DescFiles, Is.EqualTo(4));
        Assert.That(rows.Single(r => r.Name == "docs").DescFolders, Is.EqualTo(3));
    }
}
