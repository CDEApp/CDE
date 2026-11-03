using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        // Single * and ? stop at a backslash, so on a full path they match nothing here; on a name they match whole-name.
        yield return new TestCaseData("*.txt", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("na?ve.txt", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.Equals("naïve.txt", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("CAFÉ*", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.StartsWith("café", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("日*語", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.StartsWith('日') && s.EndsWith('語')));
        yield return new TestCaseData("*ファ*", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.Contains("ファ")));
        yield return new TestCaseData("\"x *.txt\"", (Func<string, string, bool, bool>)((s, p, ip) => !ip && s.StartsWith("x ", StringComparison.OrdinalIgnoreCase) && s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("*", (Func<string, string, bool, bool>)((s, p, ip) => !ip));
        yield return new TestCaseData("<*.txt | *.md> !do*", (Func<string, string, bool, bool>)((s, p, ip) => !ip &&
            (s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            && !s.StartsWith("do", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData(@"path:*\*\*.txt",
            (Func<string, string, bool, bool>)((n, p, ip) => Regex.IsMatch(p, @"^[^\\]*\\[^\\]*\\[^\\]*\.txt$", RegexOptions.IgnoreCase)));
        yield return new TestCaseData(@"path:**\*.txt",
            (Func<string, string, bool, bool>)((n, p, ip) => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData(@"path:?:\win*\*",
            (Func<string, string, bool, bool>)((n, p, ip) => Regex.IsMatch(p, @"^.:\\win[^\\]*\\[^\\]*$", RegexOptions.IgnoreCase)));
        yield return new TestCaseData("no-path:*.txt path:**\\caf\u00e9\\**",
            (Func<string, string, bool, bool>)((n, p, ip) =>
                n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && p.Contains("\\caf\u00e9\\", StringComparison.OrdinalIgnoreCase)));
        yield return new TestCaseData("case:Windows",
            (Func<string, string, bool, bool>)((n, p, ip) => (ip ? p : n).Contains("Windows", StringComparison.Ordinal)));
        yield return new TestCaseData("case:windows",
            (Func<string, string, bool, bool>)((n, p, ip) => (ip ? p : n).Contains("windows", StringComparison.Ordinal)));
        yield return new TestCaseData("no-path:case:*.TXT",
            (Func<string, string, bool, bool>)((n, p, ip) => n.EndsWith(".TXT", StringComparison.Ordinal)));
        yield return new TestCaseData("case:\"NaÏve\"",
            (Func<string, string, bool, bool>)((n, p, ip) => (ip ? p : n).Contains("NaÏve", StringComparison.Ordinal)));
        yield return new TestCaseData("no-path:case:regex:\"^NaÏve\\.TXT$\"",
            (Func<string, string, bool, bool>)((n, p, ip) => n == "NaÏve.TXT"));
        yield return new TestCaseData("case:txt !case:TXT",
            (Func<string, string, bool, bool>)((n, p, ip) => (ip ? p : n).Contains("txt", StringComparison.Ordinal) && !(ip ? p : n).Contains("TXT", StringComparison.Ordinal)));
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

    // The folders of BuildTree, listed by hand so the oracle does not read the same flag the code under test reads.
    private static readonly HashSet<string> FolderNames = new(StringComparer.Ordinal)
        { "Windows", "café", "日本語", "a|b" };

    // Oracle over (name, path, isFolder, includePath).
    public static IEnumerable<TestCaseData> FilterCases()
    {
        yield return new TestCaseData("file:",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => !dir));
        yield return new TestCaseData("folder:",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => dir));
        yield return new TestCaseData("file: txt",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => !dir && Has(ip ? p : n, "txt")));
        yield return new TestCaseData("folder:win",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => dir && Has(ip ? p : n, "win")));
        yield return new TestCaseData("!file:",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => dir));
        yield return new TestCaseData("file: folder:",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => false));
        yield return new TestCaseData("<file: txt> | <folder: café>",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) =>
                (!dir && Has(ip ? p : n, "txt")) || (dir && Has(ip ? p : n, "café"))));
        yield return new TestCaseData("file: path:windows",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => !dir && Has(p, "windows")));
        yield return new TestCaseData("folder: path:windows no-path:win",
            (Func<string, string, bool, bool, bool>)((n, p, dir, ip) => dir && Has(p, "windows") && Has(n, "win")));
    }

    [TestCaseSource(nameof(FilterCases))]
    public void FileAndFolderFilters_ReaderAndStore_MatchTheOracle(
        string query, Func<string, string, bool, bool, bool> oracle)
    {
        var store = EntryStore.Build(BuildTree());
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-evk-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            foreach (var includePath in new[] { false, true })
            {
                var expected = new List<int>();
                for (var i = 1; i < store.Count; i++)
                {
                    var name = store.FullName(i);
                    if (oracle(name, store.FullPath(i), FolderNames.Contains(name), includePath)) expected.Add(i);
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
    public void FileFilters_MatchNamesNotParentFolders_ForReaderAndStore()
    {
        // A file whose name has "doc" in a folder whose name has "obsid": the folder name must not satisfy the term.
        var root = new RootEntry { Path = @"C:\\", VolumeName = "VOL", DefaultFileName = "t.cde", DriveLetterHint = "C" };
        var obsidian = new DirEntry(true) { Path = "obsidian" };
        obsidian.AddChild(new DirEntry(false) { Path = "doc.md", Size = 1 });
        obsidian.AddChild(new DirEntry(false) { Path = "obsid doc.md", Size = 1 });
        root.AddChild(obsidian);
        root.SetInMemoryFields();
        var store = EntryStore.Build(root);
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-evf-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var options = new EntryStoreFindOptions { Pattern = "file:doc file:obsid", EverythingMode = true };
            var fromReader = new List<string>();
            reader.Find(options, i => fromReader.Add(store.FullName(i)));
            var fromStore = new List<string>();
            EntryStoreSearch.Find(store, options, i => fromStore.Add(store.FullName(i)));

            Assert.That(fromReader, Is.EqualTo(new[] { "obsid doc.md" }));
            Assert.That(fromStore, Is.EqualTo(new[] { "obsid doc.md" }));
        }
        finally { File.Delete(file); }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0);

    private static RootEntry BuildDatedTree()
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "VOL", DefaultFileName = "t.cde", DriveLetterHint = "C" };
        var logs = new DirEntry(true) { Path = "logs", Modified = new DateTime(2026, 7, 10, 8, 0, 0) };
        logs.AddChild(new DirEntry(false) { Path = "today.log", Size = 4096, Modified = new DateTime(2026, 7, 10, 11, 30, 0) });
        logs.AddChild(new DirEntry(false) { Path = "last-week.log", Size = 100, Modified = new DateTime(2026, 7, 3, 9, 0, 0) });
        logs.AddChild(new DirEntry(false) { Path = "2025-old.log", Size = 3 * 1024 * 1024, Modified = new DateTime(2025, 12, 31, 23, 59, 59) });
        var caf = new DirEntry(true) { Path = "caf\u00e9", Modified = new DateTime(2026, 7, 2, 0, 0, 0) };
        caf.AddChild(new DirEntry(false) { Path = "Na\u00cfve.TXT", Size = 2048, Modified = new DateTime(2026, 7, 2, 23, 59, 59) });
        caf.AddChild(new DirEntry(false) { Path = "empty.txt", Size = 0, Modified = new DateTime(2026, 7, 3, 0, 0, 0) });
        root.AddChild(logs);
        root.AddChild(caf);
        root.AddChild(new DirEntry(false) { Path = "readme.md", Size = 1024, Modified = new DateTime(2026, 7, 10, 12, 0, 0) });
        root.SetInMemoryFields();
        return root;
    }

    // Oracle over (name, path, size, modified, includePath); written without NumericCriterion.
    public static IEnumerable<TestCaseData> FunctionCases()
    {
        yield return new TestCaseData("size:>1kb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => size > 1024));
        yield return new TestCaseData("size:1kb..3mb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => size >= 1024 && size <= 3 * 1024 * 1024));
        yield return new TestCaseData("size:0",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => size == 0));
        yield return new TestCaseData("!size:>=1kb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => size < 1024));
        yield return new TestCaseData("dm:today",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m.Date == Now.Date));
        yield return new TestCaseData("dm:2026-07-02..2026-07-03",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m.Date >= new DateTime(2026, 7, 2) && m.Date <= new DateTime(2026, 7, 3)));
        yield return new TestCaseData("dm:2025",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m.Year == 2025));
        yield return new TestCaseData("date-modified:>2026-07-02",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m >= new DateTime(2026, 7, 3)));
        yield return new TestCaseData("dm:1day",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m >= Now.AddDays(-1)));
        yield return new TestCaseData("dm:2weeks size:<1mb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => m >= Now.AddDays(-14) && size < 1024 * 1024));
        yield return new TestCaseData("log size:>1kb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => Has(ip ? p : n, "log") && size > 1024));
        yield return new TestCaseData("path:logs size:>1kb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => Has(p, "logs") && size > 1024));
        yield return new TestCaseData("<size:0 | dm:today> path:caf\u00e9 | readme",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) =>
                ((size == 0 || m.Date == Now.Date) && Has(p, "caf\u00e9")) || Has(ip ? p : n, "readme")));
        yield return new TestCaseData("file: dm:2026-07",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => n.Contains('.') && m.Year == 2026 && m.Month == 7));
        yield return new TestCaseData("regex:^\\d{4}-",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => Regex.IsMatch(ip ? p : n, @"^\d{4}-")));
        yield return new TestCaseData("no-path:regex:\"^(today|empty)\\.\"",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => Regex.IsMatch(n, @"^(today|empty)\.", RegexOptions.IgnoreCase)));
        yield return new TestCaseData("path:regex:\"^c:\\\\caf\u00e9\\\\\" size:>1kb",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => p.StartsWith("C:\\caf\u00e9\\", StringComparison.OrdinalIgnoreCase) && size > 1024));
        yield return new TestCaseData("regex:\"na\u00cfve\\.txt$\"",
            (Func<string, string, long, DateTime, bool, bool>)((n, p, size, m, ip) => Regex.IsMatch(ip ? p : n, "na\u00efve\\.txt$", RegexOptions.IgnoreCase)));
    }

    [TestCaseSource(nameof(FunctionCases))]
    public void SizeDateAndRegexFunctions_ReaderAndStore_MatchTheOracle(
        string query, Func<string, string, long, DateTime, bool, bool> oracle)
    {
        var store = EntryStore.Build(BuildDatedTree());
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-evd-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            foreach (var includePath in new[] { false, true })
            {
                var expected = new List<int>();
                for (var i = 1; i < store.Count; i++)
                {
                    if (oracle(store.FullName(i), store.FullPath(i), store.Size[i], store.Modified(i), includePath)) expected.Add(i);
                }

                var options = new EntryStoreFindOptions
                {
                    Pattern = query, EverythingMode = true, IncludePath = includePath, Clock = new FixedClock(Now),
                };
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
