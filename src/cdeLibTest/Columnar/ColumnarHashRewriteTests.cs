using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;

namespace cdeLibTest.Columnar;

/// <summary>
/// <see cref="ColumnarFormat.WriteWithHashes"/> rewrites a .cdex with new flags and hashes straight from the
/// memory-mapped reader. The result must be byte-identical to rebuilding the tree, setting the same hashes on it
/// and writing it with the tree-based writer (the path <c>cde hash</c> used before issue #40).
/// </summary>
[TestFixture]
public class ColumnarHashRewriteTests
{
    private readonly List<string> _tempFiles = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch (IOException) { /* ignore */ }
        _tempFiles.Clear();
    }

    private string TempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cdehashrewrite-{Guid.NewGuid():N}.cdex");
        _tempFiles.Add(path);
        return path;
    }

    private static DirEntry File_(string name, long size, ulong? hash = null, bool partial = false)
    {
        var d = new DirEntry(false) { Path = name, Size = size, Modified = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc) };
        if (hash.HasValue)
        {
            d.SetHash(hash.Value);
            d.IsPartialHash = partial;
        }

        return d;
    }

    private static DirEntry Legacy(string name, long size, ulong hash) =>
        new(false) { Path = name, Size = size, BitFields = EFlags.HashDone | EFlags.PartialHash, Hash = new Hash16(hash, 0) };

    private string WriteCatalog(RootEntry root)
    {
        root.SetInMemoryFields();
        var path = TempPath();
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    private static RootEntry Catalog(bool withHashes)
    {
        var root = new RootEntry { Path = @"C:\data", VolumeName = "vol", Description = "desc" };
        var sub = new DirEntry(true) { Path = "sub" };
        root.AddChild(sub);
        root.AddChild(File_("a.bin", 100, withHashes ? 11UL : null));
        root.AddChild(File_("b.bin", 100));
        sub.AddChild(File_("c.bin", 200, withHashes ? 33UL : null, partial: true));
        sub.AddChild(File_("d.txt", 300));
        if (withHashes) sub.AddChild(Legacy("old.bin", 400, 0xBAD));
        return root;
    }

    // Apply the hash to the entry with this name, the way Duplication.CalculateHash does on a tree.
    private static void SetOnTree(RootEntry tree, string name, ulong hash, bool partial)
    {
        var e = Find(tree.Children, name);
        e.SetHash(hash);
        e.IsPartialHash = partial;
    }

    private static DirEntry Find(IEnumerable<DirEntry> kids, string name)
    {
        foreach (var k in kids)
        {
            if (k.Path == name) return k;
            if (k.IsDirectory && k.Children != null)
            {
                var found = Find(k.Children, name);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static EntryHashUpdate Update(ColumnarCatalogReader r, string name, ulong hash, bool partial)
    {
        var i = Enumerable.Range(1, r.Count - 1).First(x => r.NameOf(x) == name);
        var flags = r.Flags(i) | EFlags.HashDone;
        flags = partial ? flags | EFlags.PartialHash | EFlags.PartialHashV2 : flags & ~(EFlags.PartialHash | EFlags.PartialHashV2);
        return new EntryHashUpdate(i, flags, new Hash16(hash, 0));
    }

    private void AssertSameAsTreePath(bool sourceHasHashes, (string Name, ulong Hash, bool Partial)[] changes)
    {
        var source = WriteCatalog(Catalog(sourceHasHashes));

        // expected: the pre-#40 path - rebuild the tree, mutate it, write it.
        var tree = CatalogTreeBuilder.FromColumnarFiles([source])[0];
        foreach (var c in changes) SetOnTree(tree, c.Name, c.Hash, c.Partial);
        var expectedPath = TempPath();
        ColumnarFormat.Write(EntryStore.Build(tree), expectedPath);

        var actualPath = TempPath();
        using (var reader = new ColumnarCatalogReader(source))
        {
            var updates = changes.Select(c => Update(reader, c.Name, c.Hash, c.Partial)).OrderBy(u => u.Index).ToArray();
            ColumnarFormat.WriteWithHashes(reader, updates, source, actualPath);
        }

        Assert.That(File.ReadAllBytes(actualPath), Is.EqualTo(File.ReadAllBytes(expectedPath)));
    }

    [Test]
    public void Unhashed_catalog_gains_a_hash_column_identical_to_the_tree_path()
    {
        AssertSameAsTreePath(false, [("a.bin", 7, true), ("b.bin", 8, false), ("d.txt", 9, false)]);
    }

    [Test]
    public void Hashed_catalog_keeps_untouched_hashes_promotes_partials_and_zeroes_legacy_partials()
    {
        AssertSameAsTreePath(true, [("c.bin", 44, false), ("b.bin", 55, true)]);
    }

    [Test]
    public void No_updates_still_rewrites_identically_to_the_tree_path()
    {
        AssertSameAsTreePath(true, []);
        AssertSameAsTreePath(false, []);
    }

    [Test]
    public void Updates_out_of_index_order_are_rejected()
    {
        var source = WriteCatalog(Catalog(false));
        using var reader = new ColumnarCatalogReader(source);
        var a = Update(reader, "a.bin", 1, false);
        var b = Update(reader, "b.bin", 2, false);
        var unordered = a.Index < b.Index ? new[] { b, a } : new[] { a, b };
        Assert.Throws<ArgumentException>(() => ColumnarFormat.WriteWithHashes(reader, unordered, source, TempPath()));
    }
}
