using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// A catastrophic-backtracking user pattern must not hang a search (ReDoS): the slow entry is treated
/// as a non-match and repeated timeouts end the scan, on every regex search path. Normal patterns are
/// unaffected.
/// </summary>
[TestFixture]
public class RegexTimeoutTests
{
    // (a+)+$ against "aaaa...a!" is exponential in the number of a's.
    private const string Evil = @"^(a+)+$";
    private static readonly string EvilName = new string('a', 45) + "!";

    private static EntryStore BuildStore()
    {
        var root = new RootEntry { Path = @"C:\t", VolumeName = "V", DefaultFileName = "t.cde", DriveLetterHint = "C" };
        root.AddChild(new DirEntry(false) { Path = "aaa", Size = 1 });
        for (var i = 0; i < 8; i++)
            root.AddChild(new DirEntry(false) { Path = EvilName + i, Size = 2 });
        root.AddChild(new DirEntry(false) { Path = "readme.txt", Size = 3 });
        root.SetInMemoryFields();
        return EntryStore.Build(root);
    }

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    [Test]
    public void EntryStore_CatastrophicPattern_IsBounded_AndDoesNotThrow()
    {
        var store = BuildStore();
        var sw = Stopwatch.StartNew();
        var hits = new List<int>();
        Assert.DoesNotThrow(() => EntryStoreSearch.Find(store,
            new EntryStoreFindOptions { Pattern = Evil, RegexMode = true }, hits.Add));
        Assert.That(sw.Elapsed, Is.LessThan(Bound));
    }

    [Test]
    public void EntryStore_PatternOverload_CatastrophicPattern_IsBounded()
    {
        var store = BuildStore();
        var sw = Stopwatch.StartNew();
        var hits = new List<int>();
        Assert.DoesNotThrow(() => EntryStoreSearch.Find(store, Evil, true, false, true, true, hits.Add));
        Assert.That(sw.Elapsed, Is.LessThan(Bound));
    }

    [Test]
    public void Reader_CatastrophicPattern_IsBounded_OnBothRegexPaths()
    {
        var store = BuildStore();
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-rt-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            using var reader = new ColumnarCatalogReader(file);
            var sw = Stopwatch.StartNew();
            Assert.DoesNotThrow(() => reader.Find(new EntryStoreFindOptions { Pattern = Evil, RegexMode = true }, _ => { }));
            Assert.That(sw.Elapsed, Is.LessThan(Bound));

            sw.Restart();
            Assert.DoesNotThrow(() => reader.Find(Evil, true, false, true, true, _ => { }));
            Assert.That(sw.Elapsed, Is.LessThan(Bound));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void NormalRegex_StillReturnsSameResults()
    {
        var store = BuildStore();
        var file = Path.Combine(Path.GetTempPath(), $"cdetest-rt-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, file);
        try
        {
            var o = new EntryStoreFindOptions { Pattern = @"\.TXT$|^aaa$", RegexMode = true };
            var fromStore = new List<int>();
            EntryStoreSearch.Find(store, o, fromStore.Add);
            var fromReader = new List<int>();
            using var reader = new ColumnarCatalogReader(file);
            reader.Find(o, fromReader.Add);

            Assert.That(fromStore, Has.Count.EqualTo(2));
            Assert.That(fromReader, Is.EqualTo(fromStore));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void BoundedRegex_TimeoutIsNoMatch_ThenExhausted()
    {
        var r = new BoundedRegex(Evil);
        for (var i = 0; i < BoundedRegex.MaxTimeouts; i++)
        {
            Assert.That(r.Exhausted, Is.False);
            Assert.That(r.IsMatch(EvilName), Is.False);
        }
        Assert.That(r.Exhausted, Is.True);
        Assert.That(r.IsMatch("aaa"), Is.False, "exhausted matcher short-circuits");
        Assert.That(new BoundedRegex(Evil).IsMatch("aaa"), Is.True);
    }
}
