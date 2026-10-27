using System;
using System.IO;
using System.Security.Cryptography;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// Writing the .cdex straight from the scan tree must give exactly the bytes the
/// EntryStore.Build + ColumnarFormat.Write(store) route gives, for the same input.
/// </summary>
[TestFixture]
public class ColumnarDirectWriteTests
{
    private static readonly DateTime Stamp = new(2023, 5, 6, 7, 8, 9);

    private static RootEntry Fixture(string rootPath, bool hashes, bool bigDir = false)
    {
        var root = new RootEntry
        {
            Path = rootPath,
            VolumeName = "V\u00d6L",
            DefaultFileName = "C-VOL.cde",
            ActualFileName = "C-VOL.cdex",
            DriveLetterHint = "C",
            Description = "d\u00e9sc \u65e5\u672c",
            AvailSpace = 123456789,
            TotalSpace = 987654321,
            ScanStartUtcTicks = 638000000000000000,
            ScanEndUtcTicks = 638000000500000000,
        };

        var rnd = new Random(42);
        string[] names =
        [
            "alpha.txt", "Beta.LOG", "caf\u00e9.md", "\u65e5\u672c\u8a9e.txt", "readme", ".hidden", "a.b.c",
            "\ud83d\ude00 emoji.png", "x.", "NoExt",
        ];
        Fill(root, 0);
        root.AddChild(new DirEntry(true) { Path = "empty dir", Modified = Stamp });
        var noChildrenList = new DirEntry(true) { Path = "nokids", Modified = Stamp };
        noChildrenList.Children = null;
        root.AddChild(noChildrenList);
        if (bigDir)
        {
            var big = new DirEntry(true) { Path = "big" };
            for (var i = 0; i < 5000; i++)
                big.AddChild(new DirEntry(false) { Path = $"f{i}.dat", Size = i, Modified = Stamp.AddSeconds(i) });
            root.AddChild(big);
        }

        root.SortAllChildrenByPath();
        root.SetInMemoryFields();
        return root;

        void Fill(ICommonEntry parent, int depth)
        {
            for (var i = 0; i < 12; i++)
            {
                var isDir = depth < 3 && rnd.Next(3) == 0;
                var e = new DirEntry(isDir)
                {
                    Path = names[i % names.Length] + (isDir ? "" : i.ToString()),
                    Size = isDir ? 0 : rnd.Next(0, 100000),
                    Modified = Stamp.AddMinutes(rnd.Next(100000)),
                };
                if (i == 3) e.IsModifiedBad = true;
                if (i == 4) e.IsReparsePoint = true;
                if (hashes && !isDir && i % 3 == 0)
                {
                    e.SetHash(rnd.Next(1, int.MaxValue));
                    e.IsPartialHash = i % 2 == 0;
                }
                parent.AddChild(e);
                if (isDir) Fill(e, depth + 1);
            }
        }
    }

    private static byte[] ViaStore(RootEntry root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cdedw-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(EntryStore.Build(root), path);
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }

    private static byte[] Direct(RootEntry root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cdedw-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(root, path);
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }

    [TestCase(@"C:\test", false)]
    [TestCase(@"C:\test", true)]
    [TestCase(@"C:\", true)]
    [TestCase(@"C:\", false)]
    [TestCase("\\\\srv\\sh\\aré\\", true)]
    public void DirectWrite_IsByteIdenticalToStoreWrite(string rootPath, bool hashes)
    {
        var root = Fixture(rootPath, hashes, bigDir: true);
        Assert.That(Direct(root), Is.EqualTo(ViaStore(root)));
    }

    [Test]
    public void DirectWrite_EmptyRoot_IsByteIdentical()
    {
        var root = new RootEntry { Path = @"C:\empty" };
        root.SetInMemoryFields();
        Assert.That(Direct(root), Is.EqualTo(ViaStore(root)));
    }

    [Test]
    public void StoreWrite_Golden_Sha256()
    {
        // Pins today's bytes so a change to the (shared) layout code cannot go unnoticed.
        var hash = Convert.ToHexString(SHA256.HashData(ViaStore(Fixture(@"C:\test", true, bigDir: true))));
        Assert.That(hash, Is.EqualTo("BCD9335861F14CC59D975AD434ED3A8B3976C8244BC1E925B8616F5F594A4C4E"));
    }

    [Test]
    public void DirectWrite_ReadsBackThroughReader()
    {
        var root = Fixture(@"C:\", true);
        var path = Path.Combine(Path.GetTempPath(), $"cdedw-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(root, path);
            using var r = new ColumnarCatalogReader(path);
            var store = EntryStore.Build(root);
            Assert.That(r.Count, Is.EqualTo(store.Count));
            Assert.That(r.RootPath, Is.EqualTo(@"C:\"));
            Assert.That(r.RootFileEntryCount, Is.EqualTo(root.FileEntryCount));
        }
        finally { File.Delete(path); }
    }
}
