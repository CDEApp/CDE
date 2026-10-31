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
/// Everything-mode search over the mmap <see cref="ColumnarCatalogReader"/> and the in-memory
/// <see cref="EntryStore"/> must give the same rows, equal to an independent string-based oracle, for names and for
/// full paths, including non-ASCII text.
/// </summary>
[TestFixture]
public class EverythingSearchParityTests
{
    private static RootEntry BuildTree()
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "VOL", DefaultFileName = "t.cde", DriveLetterHint = "C" };
        var windows = new DirEntry(true) { Path = "Windows" };
        windows.AddChild(new DirEntry(false) { Path = "documents.txt", Size = 10 });
        windows.AddChild(new DirEntry(false) { Path = "notes.log", Size = 20 });
        var cafe = new DirEntry(true) { Path = "caf\u00e9" };
        cafe.AddChild(new DirEntry(false) { Path = "Na\u00cfve.TXT", Size = 30 });
        var jp = new DirEntry(true) { Path = "\u65e5\u672c\u8a9e" };
        jp.AddChild(new DirEntry(false) { Path = "\u30d5\u30a1\u30a4\u30eb.md", Size = 40 });
        var a = new DirEntry(true) { Path = "a|b" }; // operator characters in a name need quotes in a query
        a.AddChild(new DirEntry(false) { Path = "x y.txt", Size = 50 });
        root.AddChild(windows);
        root.AddChild(cafe);
        root.AddChild(jp);
        root.AddChild(a);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt", Size = 5 });
        root.SetInMemoryFields();
        return root;
    }

    private static bool Has(string s, string term) => s.Contains(term, StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<TestCaseData> Cases()
    {
        yield return new TestCaseData("do win", (Func<string, bool>)(s => Has(s, "do") && Has(s, "win")));
        yield return new TestCaseData("dow win", (Func<string, bool>)(s => Has(s, "dow") && Has(s, "win")));
        yield return new TestCaseData("txt | log", (Func<string, bool>)(s => Has(s, "txt") || Has(s, "log")));
        yield return new TestCaseData("txt !doc", (Func<string, bool>)(s => Has(s, "txt") && !Has(s, "doc")));
        yield return new TestCaseData("<txt | md> !win", (Func<string, bool>)(s => (Has(s, "txt") || Has(s, "md")) && !Has(s, "win")));
        yield return new TestCaseData("CAF\u00c9", (Func<string, bool>)(s => Has(s, "caf\u00e9")));
        yield return new TestCaseData("na\u00efve | \u65e5\u672c", (Func<string, bool>)(s => Has(s, "na\u00efve") || Has(s, "\u65e5\u672c")));
        yield return new TestCaseData("\"x y\"", (Func<string, bool>)(s => Has(s, "x y")));
        yield return new TestCaseData("\"a|b\"", (Func<string, bool>)(s => Has(s, "a|b")));
        yield return new TestCaseData("   ", (Func<string, bool>)(_ => true));
        yield return new TestCaseData("zzz", (Func<string, bool>)(_ => false));
    }

    [TestCaseSource(nameof(Cases))]
    public void ReaderAndStore_MatchTheOracle_ForNamesAndPaths(string query, Func<string, bool> oracle)
    {
        var store = EntryStore.Build(BuildTree());
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-ev-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            foreach (var includePath in new[] { false, true })
            {
                var expected = new List<int>();
                for (var i = 1; i < store.Count; i++)
                {
                    if (oracle(includePath ? store.FullPath(i) : store.FullName(i))) expected.Add(i);
                }

                var options = new EntryStoreFindOptions { Pattern = query, EverythingMode = true, IncludePath = includePath };

                var fromReader = new List<int>();
                reader.Find(options, fromReader.Add);
                var fromStore = new List<int>();
                EntryStoreSearch.Find(store, options, fromStore.Add);

                Assert.That(fromReader, Is.EqualTo(expected), $"reader '{query}' path={includePath}");
                Assert.That(fromStore, Is.EqualTo(expected), $"store '{query}' path={includePath}");
            }
        }
        finally { File.Delete(file); }
    }

    // Oracle over (name, path, includePath): path: terms use the path, no-path: terms the name, others follow includePath.
    public static IEnumerable<TestCaseData> ModifierCases()
    {
        yield return new TestCaseData("path:windows notes",
            (Func<string, string, bool, bool>)((n, p, _) => Has(p, "windows") && Has(n, "notes")));
        yield return new TestCaseData("path:windows notes",
            (Func<string, string, bool, bool>)((n, p, ip) => Has(p, "windows") && Has(ip ? p : n, "notes")));
        yield return new TestCaseData("no-path:windows",
            (Func<string, string, bool, bool>)((n, p, ip) => Has(n, "windows")));
        yield return new TestCaseData("!path:café txt",
            (Func<string, string, bool, bool>)((n, p, ip) => !Has(p, "café") && Has(ip ? p : n, "txt")));
        yield return new TestCaseData("path:<txt | 日本> !no-path:win",
            (Func<string, string, bool, bool>)((n, p, ip) => (Has(p, "txt") || Has(p, "日本")) && !Has(n, "win")));
        yield return new TestCaseData("path:\"a|b\" no-path:\"x y\"",
            (Func<string, string, bool, bool>)((n, p, ip) => Has(p, "a|b") && Has(n, "x y")));
    }

    [TestCaseSource(nameof(ModifierCases))]
    public void Modifiers_ReaderAndStore_MatchTheOracle(string query, Func<string, string, bool, bool> oracle)
    {
        var store = EntryStore.Build(BuildTree());
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-evm-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            foreach (var includePath in new[] { false, true })
            {
                var expected = new List<int>();
                for (var i = 1; i < store.Count; i++)
                {
                    if (oracle(store.FullName(i), store.FullPath(i), includePath)) expected.Add(i);
                }

                var options = new EntryStoreFindOptions { Pattern = query, EverythingMode = true, IncludePath = includePath };
                var fromReader = new List<int>();
                reader.Find(options, fromReader.Add);
                var fromStore = new List<int>();
                EntryStoreSearch.Find(store, options, fromStore.Add);

                Assert.That(fromReader, Is.EqualTo(expected), $"reader '{query}' path={includePath}");
                Assert.That(fromStore, Is.EqualTo(expected), $"store '{query}' path={includePath}");
            }
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void EverythingMode_OverridesRegexMode()
    {
        var store = EntryStore.Build(BuildTree());
        var hits = new List<int>();
        // As a regex '(' would not compile; in Everything mode it is just a character, matching nothing here.
        EntryStoreSearch.Find(store, new EntryStoreFindOptions { Pattern = "(", RegexMode = true, EverythingMode = true }, hits.Add);
        Assert.That(hits, Is.Empty);
    }
}
