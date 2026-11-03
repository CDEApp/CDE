using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// <c>dupe:</c> finds entries that share a property with another entry of ANY loaded catalog. <c>dupe:hash</c> reads
/// the stored hash only, and leaves out an entry with no full hash; the other properties do not care about hashes.
/// </summary>
[TestFixture]
public class EverythingDupeTests
{
    private static byte[] H(byte v) => Enumerable.Repeat(v, 16).ToArray();

    private static DirEntry F(string name, long size, DateTime modified, byte? hash = null, bool partial = false)
    {
        var e = new DirEntry(false) { Path = name, Size = size, Modified = modified };
        if (hash is { } h)
        {
            e.SetHash(H(h));
            if (partial) e.IsPartialHash = true;
        }

        return e;
    }

    private static readonly DateTime T1 = new(2026, 7, 1, 10, 0, 0);
    private static readonly DateTime T2 = new(2026, 7, 2, 10, 0, 0);

    private static RootEntry CatalogA()
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "A", DefaultFileName = "a.cde", DriveLetterHint = "C" };
        root.AddChild(F("a.txt", 10, T1, hash: 1));
        root.AddChild(F("b.TXT", 10, T2, hash: 2));
        root.AddChild(F("c.bin", 20, T1)); // no hash
        root.AddChild(F("d.bin", 20, T1)); // no hash
        root.AddChild(F("e.dat", 30, T1, hash: 3));
        var dir = new DirEntry(true) { Path = "dir1" };
        dir.AddChild(F("x.txt", 10, T2, hash: 1));
        root.AddChild(dir);
        root.SetInMemoryFields();
        return root;
    }

    private static RootEntry CatalogB()
    {
        var root = new RootEntry { Path = @"D:\", VolumeName = "B", DefaultFileName = "b.cde", DriveLetterHint = "D" };
        root.AddChild(F("a.txt", 10, T1, hash: 1));
        root.AddChild(F("B.txt", 11, T2, hash: 5));
        root.AddChild(F("F.dat", 30, T1, hash: 3, partial: true)); // partial hash: not a content hash
        root.AddChild(F("g.bin", 20, T2, hash: 4));
        root.AddChild(new DirEntry(true) { Path = "dir1" });
        root.SetInMemoryFields();
        return root;
    }

    private sealed record E(int Catalog, int Index, string Name, string Path, long Size, long Ticks, bool Folder, string FullHash);

    private sealed class Fixture : IDisposable
    {
        public EntryStore[] Stores { get; } = [EntryStore.Build(CatalogA()), EntryStore.Build(CatalogB())];
        public ColumnarCatalogReader[] Readers { get; }
        private readonly string[] _files;

        public Fixture()
        {
            _files = [.. Stores.Select(_ => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdetest-dupe-{Guid.NewGuid():N}.cdex"))];
            for (var i = 0; i < Stores.Length; i++) ColumnarFormat.Write(Stores[i], _files[i]);
            Readers = [.. _files.Select(f => new ColumnarCatalogReader(f))];
        }

        public List<E> Entries()
        {
            var all = new List<E>();
            for (var c = 0; c < Stores.Length; c++)
            {
                var s = Stores[c];
                for (var i = 1; i < s.Count; i++)
                {
                    var full = s.HasHash && s.IsHashDone(i) && !s.IsPartialHash(i)
                        ? s.HashOf(i).HashA + "/" + s.HashOf(i).HashB
                        : null;
                    all.Add(new E(c, i, s.FullName(i), s.FullPath(i), s.SizeOf(i), s.ModifiedOf(i).Ticks, s.IsDirectory(i), full));
                }
            }

            return all;
        }

        public HashSet<(int, int)> FindWithStores(string query)
        {
            var index = DuplicateIndex.ForPattern(query, null, Stores);
            var hits = new HashSet<(int, int)>();
            for (var c = 0; c < Stores.Length; c++)
            {
                var cat = c;
                EntryStoreSearch.Find(Stores[c], new EntryStoreFindOptions { Pattern = query, EverythingMode = true, Duplicates = index },
                    i => hits.Add((cat, i)));
            }

            return hits;
        }

        public HashSet<(int, int)> FindWithReaders(string query)
        {
            var index = DuplicateIndex.ForPattern(query, null, Readers);
            var hits = new HashSet<(int, int)>();
            for (var c = 0; c < Readers.Length; c++)
            {
                var cat = c;
                Readers[c].Find(new EntryStoreFindOptions { Pattern = query, EverythingMode = true, Duplicates = index },
                    i => hits.Add((cat, i)));
            }

            return hits;
        }

        public void Dispose()
        {
            foreach (var r in Readers) r.Dispose();
            foreach (var f in _files) File.Delete(f);
        }
    }

    // An entry is a duplicate when another entry has the same non-null key.
    private static Func<E, bool> Dup(List<E> all, Func<E, string> key) =>
        e => key(e) is { } k && all.Count(o => key(o) == k) > 1;

    private static string Lower(string s) => s.ToUpperInvariant();

    private static string ExtOf(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : string.Empty;
    }

    [Test]
    public void Every_property_matches_an_oracle_in_the_reader_and_the_store_across_two_catalogs()
    {
        using var f = new Fixture();
        var all = f.Entries();
        var cases = new (string Query, Func<E, bool> Oracle)[]
        {
            ("dupe:", Dup(all, e => Lower(e.Name))),
            ("dupe:name", Dup(all, e => Lower(e.Name))),
            ("case:dupe:", Dup(all, e => e.Name)),
            ("dupe:size", Dup(all, e => e.Size.ToString())),
            ("dupe:size;name", Dup(all, e => e.Size + "|" + Lower(e.Name))),
            ("case:dupe:size;name", Dup(all, e => e.Size + "|" + e.Name)),
            ("dupe:ext", Dup(all, e => Lower(ExtOf(e.Name)))),
            ("dupe:date-modified", Dup(all, e => e.Ticks.ToString())),
            ("dupe:dm;size", Dup(all, e => e.Ticks + "|" + e.Size)),
            ("dupe:path", Dup(all, e => Lower(e.Path))),
            ("dupe:hash", Dup(all, e => e.FullHash)),
            ("dupe:size;hash", Dup(all, e => e.FullHash is null ? null : e.Size + "|" + e.FullHash)),
            ("dupe:size file:", e => !e.Folder && Dup(all, x => x.Size.ToString())(e)),
            ("dupe:size size:>15", e => e.Size > 15 && Dup(all, x => x.Size.ToString())(e)),
            ("!dupe:size file:", e => !e.Folder && !Dup(all, x => x.Size.ToString())(e)),
            ("dupe:hash | dupe:name", e => Dup(all, x => x.FullHash)(e) || Dup(all, x => Lower(x.Name))(e)),
            ("dupe:hash dupe:size", e => Dup(all, x => x.FullHash)(e) && Dup(all, x => x.Size.ToString())(e)),
            ("dupe:size ext:bin", e => !e.Folder && ExtOf(e.Name).Equals("bin", StringComparison.OrdinalIgnoreCase) && Dup(all, x => x.Size.ToString())(e)),
        };

        foreach (var (query, oracle) in cases)
        {
            var expected = all.Where(oracle).Select(e => (e.Catalog, e.Index)).ToHashSet();
            Assert.That(f.FindWithReaders(query), Is.EquivalentTo(expected), $"readers '{query}'");
            Assert.That(f.FindWithStores(query), Is.EquivalentTo(expected), $"stores '{query}'");
        }
    }

    [Test]
    public void Dupe_hash_uses_the_stored_hash_and_leaves_out_files_without_a_full_hash()
    {
        using var f = new Fixture();
        var names = f.FindWithReaders("dupe:hash").Select(h => (h.Item1, f.Stores[h.Item1].FullName(h.Item2))).OrderBy(x => x).ToList();

        // a.txt (both catalogs) and x.txt share hash 1. e.dat and F.dat share hash 3, but F.dat's is a partial hash.
        Assert.That(names, Is.EqualTo(new[] { (0, "a.txt"), (0, "x.txt"), (1, "a.txt") }));
    }

    [Test]
    public void Dupe_on_other_properties_does_not_care_whether_a_file_is_hashed()
    {
        using var f = new Fixture();
        var bySize = f.FindWithReaders("dupe:size file:").Select(h => f.Stores[h.Item1].FullName(h.Item2)).ToList();

        // c.bin and d.bin have no hash at all and still match on size (20), and so does the hashed g.bin.
        Assert.That(bySize, Does.Contain("c.bin"));
        Assert.That(bySize, Does.Contain("d.bin"));
        Assert.That(bySize, Does.Contain("g.bin"));
        // F.dat has only a partial hash, and still matches e.dat on size (30).
        Assert.That(bySize, Does.Contain("F.dat"));
        Assert.That(f.FindWithStores("dupe:size file:").Count, Is.EqualTo(f.FindWithReaders("dupe:size file:").Count));
    }

    [Test]
    public void Dupe_hash_finds_nothing_when_no_catalog_has_hashes()
    {
        var plain = new RootEntry { Path = @"C:\", VolumeName = "A", DefaultFileName = "a.cde", DriveLetterHint = "C" };
        plain.AddChild(F("a.txt", 10, T1));
        plain.AddChild(F("b.txt", 10, T1));
        plain.SetInMemoryFields();
        var store = EntryStore.Build(plain);
        var hits = new List<int>();
        EntryStoreSearch.Find(store, new EntryStoreFindOptions { Pattern = "dupe:hash", EverythingMode = true }, hits.Add);
        Assert.That(hits, Is.Empty);

        EntryStoreSearch.Find(store, new EntryStoreFindOptions { Pattern = "dupe:size", EverythingMode = true }, hits.Add);
        Assert.That(hits, Has.Count.EqualTo(2));
    }

    [Test]
    public void Without_a_shared_index_a_catalog_is_searched_for_duplicates_within_itself()
    {
        using var f = new Fixture();
        var hits = new List<int>();
        EntryStoreSearch.Find(f.Stores[1], new EntryStoreFindOptions { Pattern = "dupe:name", EverythingMode = true }, hits.Add);
        // Catalog B alone has no two entries with the same name (a.txt and dir1 repeat only across catalogs).
        Assert.That(hits, Is.Empty);
        Assert.That(f.FindWithStores("dupe:name"), Is.Not.Empty);
    }

    [Test]
    public void Equal_dupe_functions_share_one_spec_and_different_ones_do_not()
    {
        Assert.That(EverythingQuery.Parse("dupe:size | dupe:size").DuplicateSpecs, Has.Count.EqualTo(1));
        Assert.That(EverythingQuery.Parse("dupe:size dupe:name").DuplicateSpecs, Has.Count.EqualTo(2));
        Assert.That(EverythingQuery.Parse("dupe: dupe:name").DuplicateSpecs, Has.Count.EqualTo(1));
        Assert.That(EverythingQuery.Parse("dupe:size;name dupe:name;size").DuplicateSpecs, Has.Count.EqualTo(1));
        Assert.That(EverythingQuery.Parse("dupe:name case:dupe:name").DuplicateSpecs, Has.Count.EqualTo(2));
        Assert.That(EverythingQuery.Parse("size:>1").DuplicateSpecs, Is.Empty);
    }

    [TestCase("dupe:colour", 0)]
    [TestCase("a dupe:size;nope", 2)]
    [TestCase("path:dupe:size", 0)]
    [TestCase("regex:dupe:size", 0)]
    public void A_bad_dupe_is_an_error_at_its_position(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Position, Is.EqualTo(position), ex.Message);
    }

    [Test]
    public void More_than_64_different_dupe_functions_is_an_error()
    {
        // 6 properties give 63 non-empty combinations; case: doubles the text ones, so 64 is reachable and 65 is not.
        var names = new[] { "name", "path", "ext", "size", "dm", "hash" };
        var specs = new List<string>();
        for (var mask = 1; mask < 64; mask++)
        {
            var props = string.Join(';', names.Where((_, i) => (mask >> i & 1) != 0));
            specs.Add("dupe:" + props);
            if ((mask & 7) != 0) specs.Add("case:dupe:" + props);
        }

        var query = string.Join(' ', specs);
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Message, Does.Contain("dupe:"));
    }
}
