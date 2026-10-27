using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// Characterisation tests pinning full-path building and regex search behaviour against an
/// independent string-based oracle (root-first join, no separator after a segment already ending in
/// '\' or '/'), for both the mmap <see cref="ColumnarCatalogReader"/> and the in-memory
/// <see cref="EntryStore"/>. Plus allocation guards for the single-allocation path builder and the
/// allocation-free regex scan.
/// </summary>
[TestFixture]
public class PathAndRegexCharacterisationTests
{
    private static string LongSegment => new('x', 120);

    private static RootEntry BuildTree(string rootPath)
    {
        var root = new RootEntry
        {
            Path = rootPath, VolumeName = "VOL", DefaultFileName = "t.cde", DriveLetterHint = "C",
        };

        var cafe = new DirEntry(true) { Path = "caf\u00e9" };
        cafe.AddChild(new DirEntry(false) { Path = "na\u00efve r\u00e9sum\u00e9.txt", Size = 10 });
        var jp = new DirEntry(true) { Path = "\u65e5\u672c\u8a9e" };
        jp.AddChild(new DirEntry(false) { Path = "\u30d5\u30a1\u30a4\u30eb.md", Size = 20 });
        var emoji = new DirEntry(true) { Path = "smile\U0001F600" };
        emoji.AddChild(new DirEntry(false) { Path = "\U0001F600.log", Size = 30 });
        var trailing = new DirEntry(true) { Path = "ends_with_sep\\" }; // segment already ending in '\'
        trailing.AddChild(new DirEntry(false) { Path = "after.txt", Size = 40 });

        // Deep chain of long names so the path outgrows any small initial buffer (> 2 KB).
        var deepTop = new DirEntry(true) { Path = "deep0" + LongSegment };
        var cur = deepTop;
        for (var d = 1; d < 24; d++)
        {
            var next = new DirEntry(true) { Path = "deep" + d + LongSegment };
            cur.AddChild(next);
            cur = next;
        }
        cur.AddChild(new DirEntry(false) { Path = "bottom.dat", Size = 50 });

        var plain = new DirEntry(true) { Path = "dir1" };
        plain.AddChild(new DirEntry(false) { Path = "Alpha.TXT", Size = 1 });
        plain.AddChild(new DirEntry(false) { Path = "beta.log", Size = 2 });

        root.AddChild(cafe);
        root.AddChild(jp);
        root.AddChild(emoji);
        root.AddChild(trailing);
        root.AddChild(deepTop);
        root.AddChild(plain);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt", Size = 5 });
        root.SetInMemoryFields();
        return root;
    }

    public static IEnumerable<string> Roots() =>
    [
        @"C:\test",              // root with a name, no trailing separator
        @"C:\",                  // drive root with trailing separator
        @"C:",                   // drive root without trailing separator
        @"\\server\share",       // UNC-like
        @"\\server\share\",      // UNC-like with trailing separator
        "/mnt/data",             // forward-slash root
        "/",                     // forward-slash drive-like root
    ];

    // Independent oracle: names via the store's FullName (rejoined name+ext), path via the join rule.
    private static string[] OraclePaths(EntryStore store)
    {
        var result = new string[store.Count];
        for (var i = 0; i < store.Count; i++)
        {
            var chain = new List<int>();
            for (var c = i; c != EntryStore.None; c = store.Parent[c]) chain.Add(c);
            chain.Reverse();
            var sb = new StringBuilder();
            foreach (var idx in chain)
            {
                if (sb.Length > 0 && sb[^1] != '\\' && sb[^1] != '/') sb.Append(Path.DirectorySeparatorChar);
                sb.Append(store.FullName(idx));
            }
            result[i] = sb.ToString();
        }
        return result;
    }

    private static (EntryStore store, string file) Materialise(string root)
    {
        var store = EntryStore.Build(BuildTree(root));
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-pr-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        return (store, file);
    }

    [TestCaseSource(nameof(Roots))]
    public void FullPath_MatchesOracle_ForReaderAndStore(string root)
    {
        var (store, file) = Materialise(root);
        try
        {
            var oracle = OraclePaths(store);
            using var reader = new ColumnarCatalogReader(file);
            for (var i = 0; i < store.Count; i++)
            {
                Assert.That(reader.FullPath(i), Is.EqualTo(oracle[i]), $"reader path {i}");
                Assert.That(store.FullPath(i), Is.EqualTo(oracle[i]), $"store path {i}");
                var sb = new StringBuilder();
                reader.AppendFullPath(sb, i);
                Assert.That(sb.ToString(), Is.EqualTo(oracle[i]), $"reader append {i}");
            }
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void FullPath_KnownLiterals()
    {
        var (store, file) = Materialise(@"C:\");
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var all = Enumerable.Range(0, reader.Count).Select(reader.FullPath).ToList();
            Assert.That(all[0], Is.EqualTo(@"C:\"));
            Assert.That(all, Does.Contain("C:\\caf\u00e9"));
            Assert.That(all, Does.Contain("C:\\caf\u00e9\\na\u00efve r\u00e9sum\u00e9.txt"));
            Assert.That(all, Does.Contain("C:\\\u65e5\u672c\u8a9e\\\u30d5\u30a1\u30a4\u30eb.md"));
            Assert.That(all, Does.Contain("C:\\smile\U0001F600\\\U0001F600.log"));
            Assert.That(all, Does.Contain(@"C:\ends_with_sep\after.txt"));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void FullPath_Root_IsRootName()
    {
        var (_, file) = Materialise(@"\\server\share");
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            Assert.That(reader.FullPath(0), Is.EqualTo(@"\\server\share"));
            Assert.That(reader.FullPath(1), Is.EqualTo(@"\\server\share\caf" + "\u00e9"));
        }
        finally { File.Delete(file); }
    }

    // ---- regex search ----

    private static readonly string[] Patterns =
    [
        @"alpha", @"\.txt$", @"^dir1", "caf\u00e9", "\u65e5\u672c", "\U0001F600",
        @"ends_with_sep\\after", @"deep23x+\\bottom", @"^C:\\dir1\\", @"^root_file",
    ];

    private static IEnumerable<string> ValidPatterns() => Patterns;

    [Test]
    public void RegexSearch_AllModes_MatchOracle([ValueSource(nameof(ValidPatterns))] string pattern,
        [Values(false, true)] bool includePath,
        [Values(0, 1, 2)] int fileFolderMode,
        [Values(false, true)] bool viaOptions)
    {
        var includeFiles = fileFolderMode != 1;
        var includeFolders = fileFolderMode != 2;
        var (store, file) = Materialise(@"C:\");
        try
        {
            var paths = OraclePaths(store);
            var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var expected = new List<int>();
            for (var i = 1; i < store.Count; i++)
            {
                var isDir = store.IsDirectory(i);
                if (isDir ? !includeFolders : !includeFiles) continue;
                if (rx.IsMatch(includePath ? paths[i] : store.FullName(i))) expected.Add(i);
            }

            using var reader = new ColumnarCatalogReader(file);
            var actual = new List<int>();
            if (viaOptions)
            {
                reader.Find(new EntryStoreFindOptions
                {
                    Pattern = pattern, RegexMode = true, IncludePath = includePath,
                    IncludeFiles = includeFiles, IncludeFolders = includeFolders
                }, actual.Add);
            }
            else
            {
                var n = reader.Find(pattern, regexMode: true, includePath, includeFiles, includeFolders, actual.Add);
                Assert.That(n, Is.EqualTo(actual.Count));
            }
            Assert.That(actual, Is.EqualTo(expected), "reader");

            var viaStore = new List<int>();
            if (viaOptions)
                EntryStoreSearch.Find(store, new EntryStoreFindOptions
                {
                    Pattern = pattern, RegexMode = true, IncludePath = includePath,
                    IncludeFiles = includeFiles, IncludeFolders = includeFolders
                }, viaStore.Add);
            else
                EntryStoreSearch.Find(store, pattern, true, includePath, includeFiles, includeFolders, viaStore.Add);
            Assert.That(viaStore, Is.EqualTo(expected), "store");
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void RegexSearch_NonAsciiCaseInsensitive()
    {
        var (_, file) = Materialise(@"C:\");
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var hits = new List<string>();
            reader.Find(new EntryStoreFindOptions { Pattern = "CAF\u00c9", RegexMode = true, IncludePath = false },
                i => hits.Add(reader.FullPath(i)));
            Assert.That(hits, Is.EqualTo(new[] { "C:\\caf\u00e9" }));
        }
        finally { File.Delete(file); }
    }

    // ---- allocation guards ----

    private static long Best(Action action, int runs = 5)
    {
        action(); // warm up (JIT, tiering, regex)
        var best = long.MaxValue;
        for (var r = 0; r < runs; r++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return best;
    }

    private static long StringBytes(int chars) => (22L + 2 * chars + 7) & ~7L; // x64 string object size

    [Test]
    public void ReaderFullPath_AllocatesOnlyTheResultString([Values(@"C:\", @"C:\test", @"\\server\share")] string root)
    {
        var (store, file) = Materialise(root);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var oracle = OraclePaths(store);
            for (var i = 0; i < store.Count; i++)
            {
                string result = null;
                var bytes = Best(() => result = reader.FullPath(i), 3);
                Assert.That(bytes, Is.LessThanOrEqualTo(StringBytes(result.Length)),
                    $"entry {i} ({result.Length} chars) allocated {bytes} bytes");
                Assert.That(result, Is.EqualTo(oracle[i]));
            }
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void StoreFullPath_AllocatesOnlyTheResultString()
    {
        var (store, file) = Materialise(@"C:\");
        try
        {
            for (var i = 0; i < store.Count; i++)
            {
                string result = null;
                var bytes = Best(() => result = store.FullPath(i), 3);
                Assert.That(bytes, Is.LessThanOrEqualTo(StringBytes(result.Length)),
                    $"entry {i} ({result.Length} chars) allocated {bytes} bytes");
            }
        }
        finally { File.Delete(file); }
    }

    private static string BigCatalog(int files, out EntryStore store)
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "V", DefaultFileName = "b.cde", DriveLetterHint = "C" };
        var dirs = Math.Max(1, files / 100);
        for (var d = 0; d < dirs; d++)
        {
            var dir = new DirEntry(true) { Path = $"directory_number_{d:D6}" };
            for (var f = 0; f < 100; f++)
                dir.AddChild(new DirEntry(false) { Path = $"some_reasonably_long_file_name_{d:D6}_{f:D3}.dat", Size = f });
            root.AddChild(dir);
        }
        root.SetInMemoryFields();
        store = EntryStore.Build(root);
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-big-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        return file;
    }

    // Allocation must not scale with entries scanned: compare a small and a large catalog; the
    // difference (a per-entry cost) must be tiny. Regex construction cost cancels out.
    [Test]
    public void ReaderRegex_DoesNotAllocatePerEntryScanned([Values(false, true)] bool includePath,
        [Values(false, true)] bool viaOptions)
    {
        var small = BigCatalog(2_000, out _);
        var large = BigCatalog(80_000, out _);
        try
        {
            using var rs = new ColumnarCatalogReader(small);
            using var rl = new ColumnarCatalogReader(large);
            const string pattern = "no_such_text_anywhere";

            long Run(ColumnarCatalogReader r) => Best(() =>
            {
                if (viaOptions)
                    r.Find(new EntryStoreFindOptions { Pattern = pattern, RegexMode = true, IncludePath = includePath },
                        _ => { });
                else
                    r.Find(pattern, true, includePath, true, true);
            }, 3);

            var a = Run(rs);
            var b = Run(rl);
            var extraEntries = rl.Count - rs.Count;
            Assert.That(b - a, Is.LessThan(extraEntries / 10), // < 0.1 byte per extra entry scanned
                $"small={a} large={b} extraEntries={extraEntries}");
        }
        finally { File.Delete(small); File.Delete(large); }
    }

    [Test]
    public void StoreRegex_DoesNotAllocatePerEntryScanned([Values(false, true)] bool includePath)
    {
        var f1 = BigCatalog(2_000, out var small);
        var f2 = BigCatalog(80_000, out var large);
        try
        {
            const string pattern = "no_such_text_anywhere";
            long Run(EntryStore s) => Best(() => EntryStoreSearch.Find(s, new EntryStoreFindOptions
                { Pattern = pattern, RegexMode = true, IncludePath = includePath }, _ => { }), 3);
            var a = Run(small);
            var b = Run(large);
            var extraEntries = large.Count - small.Count;
            Assert.That(b - a, Is.LessThan(extraEntries / 10), $"small={a} large={b} extraEntries={extraEntries}");
        }
        finally { File.Delete(f1); File.Delete(f2); }
    }
}
