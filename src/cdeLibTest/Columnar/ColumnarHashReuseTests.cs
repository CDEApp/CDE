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
/// Hash reuse on rescan, walking the old memory-mapped catalog by index. The previous behaviour
/// (rebuild the old tree with <see cref="CatalogTreeBuilder"/> then <c>TraverseTreesCopyHash</c>) is
/// the oracle: both must leave identical hashes and flags in the fresh tree.
/// </summary>
[TestFixture]
public class ColumnarHashReuseTests
{
    private static readonly string[] Names =
    [
        "alpha.txt", "Beta.LOG", "caf\u00e9.md", "\u65e5\u672c\u8a9e.txt", "readme", ".hidden", "a.b.c", "Z\u00dc.bin",
        "data.dat", "sub", "Docs", "\u00e9t\u00e9",
    ];

    private static readonly DateTime Stamp = new(2022, 3, 4, 5, 6, 7);

    private static RootEntry BuildTree(int seed, bool hashed, bool mutate, bool destHashes)
    {
        var rnd = new Random(seed); // same seed => same shape for old and fresh
        var mut = new Random(seed * 31 + 7);
        var root = new RootEntry { Path = @"C:\test", VolumeName = "V", DefaultFileName = "x.cde" };
        Fill(root, 0);
        root.SetInMemoryFields();
        return root;

        void Fill(ICommonEntry parent, int depth)
        {
            var n = rnd.Next(3, 9);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < n; i++)
            {
                var name = Names[rnd.Next(Names.Length)];
                var isDir = depth < 3 && rnd.Next(3) == 0;
                if (!used.Add(name)) continue; // unique names per directory
                var size = rnd.Next(0, 5) * 100L;
                var mod = Stamp.AddMinutes(rnd.Next(0, 4));
                var hashKind = rnd.Next(4); // 0 none, 1 partial, 2 full, 3 full
                var destKind = rnd.Next(4);
                if (mutate)
                {
                    // Fresh scan drift: some entries changed size/time, some renamed by case.
                    var m = mut.Next(8);
                    if (m == 0) size += 1;
                    else if (m == 1) mod = mod.AddSeconds(1);
                    else if (m == 2) name = name.ToUpperInvariant();
                    else if (m == 3) name = "renamed-" + name;
                }

                var e = new DirEntry(isDir) { Path = name, Size = isDir ? 0 : size, Modified = mod };
                if (!isDir && hashed && hashKind > 0)
                {
                    e.SetHash(rnd.Next(1, 1000000));
                    e.IsPartialHash = hashKind == 1;
                }
                else if (!isDir && destHashes && destKind > 0)
                {
                    e.SetHash(5000000 + rnd.Next(1000));
                    e.IsPartialHash = destKind == 1;
                }

                parent.AddChild(e);
                if (isDir) Fill(e, depth + 1);
            }
        }
    }

    private static string WriteCdex(RootEntry root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cdehr-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    private static List<(string Path, byte Flags, Hash16 Hash)> Snapshot(RootEntry re)
    {
        var res = new List<(string, byte, Hash16)>();
        void Walk(IReadOnlyList<ICommonEntry> kids, string prefix)
        {
            if (kids == null) return;
            foreach (var k in kids)
            {
                var p = prefix + "/" + k.Path;
                res.Add((p, (byte)((DirEntry)k).BitFields, k.IsHashDone ? k.Hash : default));
                if (k.IsDirectory) Walk(k.Children, p);
            }
        }
        Walk(((ICommonEntry)re).Children, "");
        return res;
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void CopyHashes_MatchesTreeRebuildOracle(int seed)
    {
        var path = WriteCdex(BuildTree(seed, hashed: true, mutate: false, destHashes: false));
        try
        {
            var viaOldTree = BuildTree(seed, hashed: false, mutate: true, destHashes: true);
            var viaReader = BuildTree(seed, hashed: false, mutate: true, destHashes: true);

            CatalogTreeBuilder.FromColumnarFiles([path])[0].TraverseTreesCopyHash(viaOldTree);
            using (var reader = new ColumnarCatalogReader(path))
                ColumnarHashReuse.CopyHashes(reader, viaReader);

            var expected = Snapshot(viaOldTree);
            Assert.That(expected.Count(e => e.Flags != 0 && (e.Flags & (byte)Flags.HashDone) != 0), Is.GreaterThan(0),
                "fixture should reuse at least one hash");
            Assert.That(Snapshot(viaReader), Is.EqualTo(expected));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void CopyHashes_SetsHashDoneFlag_AndRequiresSameSizeAndModified()
    {
        RootEntry Make(long size, DateTime mod, bool hash)
        {
            var root = new RootEntry { Path = @"C:\test" };
            var f = new DirEntry(false) { Path = "a.txt", Size = size, Modified = mod };
            if (hash) f.SetHash([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            root.AddChild(f);
            root.SetInMemoryFields();
            return root;
        }

        var path = WriteCdex(Make(100, Stamp, hash: true));
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            var same = Make(100, Stamp, hash: false);
            ColumnarHashReuse.CopyHashes(reader, same);
            Assert.That(same.Children[0].IsHashDone, Is.True);
            Assert.That(same.Children[0].Hash.IsSet, Is.True);

            var diffSize = Make(101, Stamp, hash: false);
            ColumnarHashReuse.CopyHashes(reader, diffSize);
            Assert.That(diffSize.Children[0].IsHashDone, Is.False);

            var diffTime = Make(100, Stamp.AddSeconds(1), hash: false);
            ColumnarHashReuse.CopyHashes(reader, diffTime);
            Assert.That(diffTime.Children[0].IsHashDone, Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void CopyHashes_DifferentRootPath_Throws()
    {
        var path = WriteCdex(BuildTree(1, true, false, false));
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            var other = new RootEntry { Path = @"D:\other" };
            Assert.Throws<ArgumentException>(() => ColumnarHashReuse.CopyHashes(reader, other));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void CopyHashes_NeverRebuildsOldTree_AllocatesLittlePerEntry()
    {
        static RootEntry Large(bool hashed)
        {
            var root = new RootEntry { Path = @"C:\test" };
            for (var d = 0; d < 200; d++)
            {
                var dir = new DirEntry(true) { Path = "dir" + d };
                for (var f = 0; f < 50; f++)
                {
                    var e = new DirEntry(false) { Path = $"file{f}.txt", Size = f, Modified = Stamp };
                    if (hashed) e.SetHash(d * 100 + f + 1);
                    dir.AddChild(e);
                }
                root.AddChild(dir);
            }
            root.SetInMemoryFields();
            return root;
        }

        var path = WriteCdex(Large(hashed: true));
        try
        {
            var fresh = Large(hashed: false);
            using var reader = new ColumnarCatalogReader(path);
            ColumnarHashReuse.CopyHashes(reader, Large(false)); // warm up

            var before = GC.GetAllocatedBytesForCurrentThread();
            ColumnarHashReuse.CopyHashes(reader, fresh);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // A rebuilt pointer tree would cost hundreds of bytes per entry; the index walk only pays
            // for the reused lookup structure.
            Assert.That(allocated, Is.LessThan(reader.Count * 64L), $"allocated {allocated} bytes for {reader.Count} entries");
        }
        finally { File.Delete(path); }
    }
}
