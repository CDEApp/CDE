using System;
using System.IO;
using System.Linq;
using System.Threading;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>#235 A1: a scan on several threads writes the same catalog as a scan on one.</summary>
[TestFixture]
public class ParallelScanTests
{
    private string _dir;
    private string _data;

    [OneTimeSetUp]
    public void BuildTree()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cde-parscan-{Guid.NewGuid():N}");
        _data = Path.Combine(_dir, "data");
        var rng = new Random(235);
        MakeLevel(_data, depth: 0, rng);
        Directory.CreateDirectory(Path.Combine(_data, "node_modules", "pkg"));
        File.WriteAllText(Path.Combine(_data, "node_modules", "pkg", "index.js"), "x");
    }

    private static void MakeLevel(string dir, int depth, Random rng)
    {
        Directory.CreateDirectory(dir);
        for (var f = 0; f < 15; f++)
        {
            File.WriteAllBytes(Path.Combine(dir, $"file{f}.bin"), new byte[rng.Next(0, 300)]);
        }

        if (depth == 3) return;
        for (var d = 0; d < 4; d++)
        {
            MakeLevel(Path.Combine(dir, $"dir{d}"), depth + 1, rng);
        }
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private byte[] ScanBytes(int threads, ScanExcludeMatcher excludes = null)
    {
        var tree = new RootEntry();
        tree.PopulateRoot(_data, excludes: excludes, threads: threads);
        tree.SortAllChildrenByPath();
        tree.SetSummaryFields();
        tree.ScanStartUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc); // the only fields that differ by run
        tree.ScanEndUtc = tree.ScanStartUtc;
        tree.AvailSpace = 0; // free space moves while the test writes files
        var path = Path.Combine(_dir, $"scan-{threads}-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.WriteDirect(tree, path);
        try { return File.ReadAllBytes(path); }
        finally { File.Delete(path); }
    }

    [TestCase(2)]
    [TestCase(8)]
    public void ParallelScan_WritesTheSameCatalogAsASerialScan(int threads)
    {
        var serial = ScanBytes(1);
        Assert.That(ScanBytes(threads), Is.EqualTo(serial));
    }

    [Test]
    public void ParallelScan_WithoutSorting_KeepsEachFolderInEnumerationOrder()
    {
        static string Shape(RootEntry r)
        {
            var lines = new System.Collections.Generic.List<string>();
            r.TraverseTreePair((p, d) => { lines.Add($"{p.FullPath}|{d.Path}|{d.Size}"); return true; });
            lines.Sort(StringComparer.Ordinal);
            return string.Join('\n', lines);
        }

        var a = new RootEntry();
        a.PopulateRoot(_data, threads: 1);
        var b = new RootEntry();
        b.PopulateRoot(_data, threads: 8);
        Assert.That(Shape(b), Is.EqualTo(Shape(a)));
        Assert.That(b.FileEntryCount + b.DirEntryCount, Is.EqualTo(a.FileEntryCount + a.DirEntryCount));
    }

    [Test]
    public void ParallelScan_CountsExcludedFoldersLikeASerialScan()
    {
        var excludes = ScanExcludeMatcher.ForScanConfig(new ScanSection { ExcludePaths = ["node_modules"] });
        var serial = new RootEntry();
        serial.PopulateRoot(_data, excludes: excludes, threads: 1);
        var parallel = new RootEntry();
        parallel.PopulateRoot(_data, excludes: excludes, threads: 8);

        Assert.That(parallel.ExcludedFolderCount, Is.EqualTo(1));
        Assert.That(parallel.ExcludedFolderCount, Is.EqualTo(serial.ExcludedFolderCount));
        Assert.That(ScanBytes(8, excludes), Is.EqualTo(ScanBytes(1, excludes)));
    }

    [TestCase(1)]
    [TestCase(8)]
    public void ScanReportsEveryEntryInItsFinalCount(int threads)
    {
        var last = 0;
        var tree = new RootEntry { SimpleScanCountEvent = (n, _) => last = n };
        tree.PopulateRoot(_data, threads: threads);
        Assert.That(last, Is.EqualTo(tree.FileEntryCount + tree.DirEntryCount));
    }

    [TestCase(1)]
    [TestCase(8)]
    public void ACancelThrownFromProgress_StopsTheScanAndReachesTheCaller(int threads)
    {
        using var cts = new CancellationTokenSource();
        var tree = new RootEntry
        {
            SimpleScanCountEvent = (_, _) =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }
        };

        // A batch of 1000 entries raises the event; make sure the fixture is big enough to reach it.
        Assume.That(Directory.EnumerateFileSystemEntries(_data, "*", SearchOption.AllDirectories).Count(),
            Is.GreaterThanOrEqualTo(1000));
        Assert.Throws<OperationCanceledException>(() => tree.PopulateRoot(_data, token: cts.Token, threads: threads));
    }
}
