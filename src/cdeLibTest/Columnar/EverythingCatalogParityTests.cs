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
/// <c>catalog:</c> over the mmap <see cref="ColumnarCatalogReader"/> and the in-memory <see cref="EntryStore"/>, over two
/// catalogs with different file names, must give the rows an independent oracle gives.
/// </summary>
[TestFixture]
public class EverythingCatalogParityTests
{
    private static RootEntry Tree(string stored, string diskName, params (string Name, long Size, byte? Hash)[] files)
    {
        var root = new RootEntry
        {
            Path = @"C:\", VolumeName = "V", DefaultFileName = stored, ActualFileName = diskName, DriveLetterHint = "C",
        };
        foreach (var (name, size, hash) in files)
        {
            var file = new DirEntry(false) { Path = name, Size = size };
            if (hash is { } h) file.SetHash(Enumerable.Repeat(h, 16).ToArray());
            root.AddChild(file);
        }

        root.SetInMemoryFields();
        return root;
    }

    private sealed record Row(int Catalog, string CatalogName, string Name, long Size, string Hash);

    private sealed class Fixture : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("cdetest-catalog-").FullName;
        public string[] Names { get; } = ["photos.cdex", "Backup-2026.cdex"];
        public EntryStore[] Stores { get; }
        public ColumnarCatalogReader[] Readers { get; }

        public Fixture()
        {
            // The stored name is stale on purpose: the file name on disk is what catalog: must use.
            Stores =
            [
                EntryStore.Build(Tree("stale-one.cde", Names[0], ("a.txt", 10, 1), ("b.log", 20, null), ("c.mp3", 30, 3))),
                EntryStore.Build(Tree("stale-two.cde", Names[1], ("a.txt", 10, 1), ("d.txt", 40, null), ("e.log", 20, 5))),
            ];
            Readers = [.. Stores.Select((s, i) =>
            {
                var path = Path.Combine(_dir, Names[i]);
                ColumnarFormat.Write(s, path);
                return new ColumnarCatalogReader(path);
            })];
        }

        public List<Row> Rows()
        {
            var rows = new List<Row>();
            for (var c = 0; c < Stores.Length; c++)
            {
                for (var i = 1; i < Stores[c].Count; i++)
                {
                    var s = Stores[c];
                    rows.Add(new Row(c, Names[c], s.FullName(i), s.SizeOf(i),
                        s.IsHashDone(i) ? s.HashOf(i).HashA + "/" + s.HashOf(i).HashB : null));
                }
            }

            return rows;
        }

        public HashSet<(int, string)> FromReaders(string query)
        {
            var index = cdeLib.DuplicateIndex.ForPattern(query, null, Readers);
            var hits = new HashSet<(int, string)>();
            for (var c = 0; c < Readers.Length; c++)
            {
                var cat = c;
                Readers[c].Find(new EntryStoreFindOptions { Pattern = query, EverythingMode = true, Duplicates = index },
                    i => hits.Add((cat, Readers[cat].FullName(i))));
            }

            return hits;
        }

        public HashSet<(int, string)> FromStores(string query)
        {
            var index = cdeLib.DuplicateIndex.ForPattern(query, null, Stores);
            var hits = new HashSet<(int, string)>();
            for (var c = 0; c < Stores.Length; c++)
            {
                var cat = c;
                EntryStoreSearch.Find(Stores[c], new EntryStoreFindOptions { Pattern = query, EverythingMode = true, Duplicates = index },
                    i => hits.Add((cat, Stores[cat].FullName(i))));
            }

            return hits;
        }

        public void Dispose()
        {
            foreach (var r in Readers) r.Dispose();
            try { Directory.Delete(_dir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    [Test]
    public void ReaderAndStore_MatchTheOracle_AcrossTwoCatalogs()
    {
        using var f = new Fixture();
        var rows = f.Rows();
        bool Has(string text, string term) => text.Contains(term, StringComparison.OrdinalIgnoreCase);
        bool Dup<T>(Row r, Func<Row, T> key) => key(r) is { } k && rows.Count(o => Equals(key(o), k)) > 1;

        var cases = new (string Query, Func<Row, bool> Oracle)[]
        {
            ("catalog:photos", r => Has(r.CatalogName, "photos")),
            ("catalog:BACKUP", r => Has(r.CatalogName, "backup")),
            ("catalog:stale", _ => false),
            ("!catalog:photos", r => !Has(r.CatalogName, "photos")),
            ("catalog:*.cdex", _ => true),
            ("catalog:photos.cdex", r => r.CatalogName == "photos.cdex"),
            ("catalog:Backup*", r => r.CatalogName.StartsWith("Backup", StringComparison.OrdinalIgnoreCase)),
            ("case:catalog:backup", _ => false),
            ("case:catalog:Backup", r => r.Catalog == 1),
            ("regex:catalog:^photos", r => r.Catalog == 0),
            ("regex:catalog:\"-\\d{4}\\.cdex$\"", r => r.Catalog == 1),
            ("catalog:photos ext:txt", r => r.Catalog == 0 && r.Name.EndsWith(".txt")),
            ("catalog:backup ext:log", r => r.Catalog == 1 && r.Name.EndsWith(".log")),
            ("catalog:photos | ext:log", r => r.Catalog == 0 || r.Name.EndsWith(".log")),
            ("catalog:photos size:>15", r => r.Catalog == 0 && r.Size > 15),
            ("catalog:photos a", r => r.Catalog == 0 && Has(r.Name, "a")),
            ("catalog:<photos | backup> a", r => Has(r.Name, "a")),
            // dupe: still counts over BOTH catalogs; catalog: only narrows what is listed.
            ("catalog:photos dupe:size", r => r.Catalog == 0 && Dup(r, x => x.Size)),
            ("catalog:photos dupe:hash", r => r.Catalog == 0 && Dup(r, x => x.Hash)),
            ("catalog:backup dupe:name", r => r.Catalog == 1 && Dup(r, x => x.Name.ToUpperInvariant())),
            ("!catalog:photos dupe:size", r => r.Catalog == 1 && Dup(r, x => x.Size)),
        };

        foreach (var (query, oracle) in cases)
        {
            var expected = rows.Where(oracle).Select(r => (r.Catalog, r.Name)).ToHashSet();
            Assert.That(f.FromReaders(query), Is.EquivalentTo(expected), $"readers '{query}'");
            Assert.That(f.FromStores(query), Is.EquivalentTo(expected), $"stores '{query}'");
        }
    }

    [Test]
    public void The_options_catalog_name_overrides_the_catalogs_own_file_name()
    {
        using var f = new Fixture();
        var hits = new List<int>();
        var options = new EntryStoreFindOptions { Pattern = "catalog:renamed", EverythingMode = true, CatalogName = "renamed.cdex" };
        f.Readers[0].Find(options, hits.Add);
        Assert.That(hits, Has.Count.EqualTo(3));

        hits.Clear();
        EntryStoreSearch.Find(f.Stores[0], options, hits.Add);
        Assert.That(hits, Has.Count.EqualTo(3));

        hits.Clear();
        options.CatalogName = "other.cdex";
        f.Readers[0].Find(options, hits.Add);
        EntryStoreSearch.Find(f.Stores[0], options, hits.Add);
        Assert.That(hits, Is.Empty);
    }
}
