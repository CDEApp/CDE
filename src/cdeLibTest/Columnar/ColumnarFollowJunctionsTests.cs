using System;
using System.Buffers.Binary;
using System.IO;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// #66: the scan option FollowJunctions is persisted in the .cdex header flags (bit1) so a rescan can
/// reproduce the original scan. Absent bit (all pre-#66 files) reads as false.
/// </summary>
[TestFixture]
public class ColumnarFollowJunctionsTests
{
    private static RootEntry Tree(bool follow, bool hashed = false)
    {
        var root = new RootEntry { Path = @"C:\test", VolumeName = "VOL", FollowJunctions = follow };
        var f = new DirEntry(false) { Path = "a.txt", Size = 10 };
        if (hashed) f.SetHash(12345);
        root.AddChild(f);
        root.SetInMemoryFields();
        return root;
    }

    private static string Temp() => Path.Combine(Path.GetTempPath(), $"cdefj-{Guid.NewGuid():N}.cdex");

    private static T WithFile<T>(Action<string> write, Func<ColumnarCatalogReader, T> read)
    {
        var path = Temp();
        try
        {
            write(path);
            using var r = new ColumnarCatalogReader(path);
            return read(r);
        }
        finally { File.Delete(path); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StoreWrite_RoundTrips(bool follow)
        => Assert.That(WithFile(p => ColumnarFormat.Write(EntryStore.Build(Tree(follow)), p), r => r.FollowJunctions),
            Is.EqualTo(follow));

    [TestCase(true)]
    [TestCase(false)]
    public void DirectWrite_RoundTrips(bool follow)
        => Assert.That(WithFile(p => ColumnarFormat.Write(Tree(follow), p), r => r.FollowJunctions),
            Is.EqualTo(follow));

    [Test]
    public void StoreAndDirectWrite_AreByteIdentical_WhenFollowing()
    {
        var a = Temp();
        var b = Temp();
        try
        {
            var root = Tree(true, hashed: true);
            ColumnarFormat.Write(EntryStore.Build(root), a);
            ColumnarFormat.Write(root, b);
            Assert.That(File.ReadAllBytes(b), Is.EqualTo(File.ReadAllBytes(a)));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Test]
    public void FollowJunctions_DoesNotDisturbHasHashes()
    {
        var (fj, hh) = WithFile(p => ColumnarFormat.Write(Tree(true, hashed: true), p),
            r => (r.FollowJunctions, r.HasHashes));
        Assert.That(fj, Is.True);
        Assert.That(hh, Is.True);
    }

    [Test]
    public void LegacyFile_WithoutFlag_ReadsAsFalse_AndUsesVersion1()
    {
        // A false flag writes exactly the pre-#66 bytes (version 1, flags bit1 clear), so this is a v1 file.
        var path = Temp();
        try
        {
            ColumnarFormat.Write(Tree(false), path);
            var bytes = File.ReadAllBytes(path);
            Assert.That(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)), Is.EqualTo(1));
            Assert.That(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)) & ColumnarFormat.FlagFollowJunctions, Is.Zero);
            using var r = new ColumnarCatalogReader(path);
            Assert.That(r.FollowJunctions, Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void FollowingFile_KeepsVersion1_SoOlderBuildsStillLoadIt()
    {
        var path = Temp();
        try
        {
            ColumnarFormat.Write(Tree(true), path);
            var bytes = File.ReadAllBytes(path);
            Assert.That(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)), Is.EqualTo(1));
        }
        finally { File.Delete(path); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RealScan_RecordsOptionUsed(bool follow)
    {
        // Both CreateCacheCommandHandlers scan via PopulateRoot(path, followJunctions) then ColumnarFormat.Write(root).
        var dir = Path.Combine(Path.GetTempPath(), $"cdefj-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "x.txt"), "x");
        try
        {
            var root = new RootEntry();
            root.PopulateRoot(dir, follow);
            Assert.That(WithFile(p => ColumnarFormat.Write(root, p), r => r.FollowJunctions), Is.EqualTo(follow));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HashRewritePath_PreservesFlag(bool follow)
    {
        // cde hash: .cdex -> CatalogTreeBuilder tree -> EntryStore.Build -> ColumnarFormat.Write
        var path = Temp();
        try
        {
            ColumnarFormat.Write(Tree(follow), path);
            var rebuilt = CatalogTreeBuilder.FromColumnarFiles([path])[0];
            Assert.That(rebuilt.FollowJunctions, Is.EqualTo(follow));
            ColumnarFormat.Write(EntryStore.Build(rebuilt), path);
            using var r = new ColumnarCatalogReader(path);
            Assert.That(r.FollowJunctions, Is.EqualTo(follow));
        }
        finally { File.Delete(path); }
    }
}
