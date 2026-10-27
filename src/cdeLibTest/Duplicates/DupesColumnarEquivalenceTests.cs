using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest.Duplicates;

/// <summary>
/// Characterisation tests: the index-based duplicate finder over memory-mapped .cdex readers must
/// produce exactly the same output (log lines, set order, paths within sets) as the original
/// tree-based <c>CatalogTreeBuilder.FromColumnarFiles</c> + <c>Duplication.FindDuplicates</c> path.
/// </summary>
[TestFixture]
[NonParallelizable]
public class DupesColumnarEquivalenceTests
{
    private sealed class RecordingLogger(TextWriter sink) : ILogger
    {
        public void LogException(Exception ex, string format, params object[] paramList) =>
            sink.WriteLine("EX: " + string.Format(format, paramList));

        public void LogInfo(string format, params object[] paramList) =>
            sink.WriteLine("INFO: " + string.Format(format, paramList));

        public void LogDebug(string format, params object[] paramList) { }
    }

    private readonly List<string> _tempFiles = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch { /* ignore */ }
        _tempFiles.Clear();
    }

    private string Write(RootEntry root)
    {
        root.SetInMemoryFields();
        var path = Path.Combine(Path.GetTempPath(), $"cdedupes-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        _tempFiles.Add(path);
        return path;
    }

    private static Duplication NewDuplication(ILogger logger)
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(5000);
        return new Duplication(logger, config, Substitute.For<IApplicationDiagnostics>(), new OperationCancellation());
    }

    private static string RunOld(IList<string> files)
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            NewDuplication(new RecordingLogger(sw)).FindDuplicates(CatalogTreeBuilder.FromColumnarFiles(files));
        }
        finally { Console.SetOut(original); }
        return sw.ToString();
    }

    private static string RunNew(IList<string> files, int? sizesPerPass = null)
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        var readers = files.Select(f => new ColumnarCatalogReader(f)).ToList();
        try
        {
            if (sizesPerPass.HasValue)
                new ColumnarDuplicateFinder(new RecordingLogger(sw), sizesPerPass.Value).FindDuplicates(readers);
            else
                NewDuplication(new RecordingLogger(sw)).FindDuplicates(readers);
        }
        finally
        {
            Console.SetOut(original);
            foreach (var r in readers) r.Dispose();
        }
        return sw.ToString();
    }

    private static DirEntry File_(string name, long size, int? hash = null, bool partial = false)
    {
        var d = new DirEntry(false) { Path = name, Size = size };
        if (hash.HasValue) d.SetHash(hash.Value);
        d.IsPartialHash = partial;
        return d;
    }

    private static void AssertSame(IList<string> files, bool expectDuplicates = true, int? sizesPerPass = null)
    {
        var expected = RunOld(files);
        // Guard against a vacuous comparison: the fixture must actually produce duplicate sets.
        Assert.That(expected.Contains("--------------------------------------"), Is.EqualTo(expectDuplicates));
        var actual = RunNew(files, sizesPerPass);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void MultipleCatalogs_MixedHashStates_MatchesTreeBasedOutput()
    {
        var a = new RootEntry { Path = @"C:\data" };
        var dirA = new DirEntry(true) { Path = "a" };
        var dirB = new DirEntry(true) { Path = "b" };
        dirA.AddChild(File_("x.txt", 100, 1));
        dirA.AddChild(File_("same-size-diff-hash.bin", 100, 2));
        dirA.AddChild(File_("partial.bin", 100, 1, partial: true));
        dirA.AddChild(File_("unhashed.bin", 100));
        dirB.AddChild(File_("y.txt", 100, 1));
        dirB.AddChild(File_("zero1.txt", 0, 5));
        dirB.AddChild(File_("zero2.txt", 0, 5));
        dirB.AddChild(File_("ünï-çødé-日本.txt", 100, 1));
        dirB.AddChild(File_("other.bin", 100, 2));
        var emptyDir = new DirEntry(true) { Path = "empty" };
        a.AddChild(dirA);
        a.AddChild(dirB);
        a.AddChild(emptyDir);
        a.AddChild(File_("root.txt", 7, 9));

        var b = new RootEntry { Path = @"C:\" };
        var sub = new DirEntry(true) { Path = "sub" };
        sub.AddChild(File_("copy.txt", 100, 1));
        sub.AddChild(File_("big1.iso", 5000, 3));
        b.AddChild(sub);
        b.AddChild(File_("big2.iso", 5000, 3));
        b.AddChild(File_("single.dat", 12345, 77));
        b.AddChild(File_("root-dup.txt", 7, 9));

        var c = new RootEntry { Path = @"\\srv\share" };
        c.AddChild(File_("big3.iso", 5000, 3));
        c.AddChild(File_("big3-partial.iso", 5000, 3, partial: true));
        c.AddChild(File_("small.txt", 7, 9));

        AssertSame([Write(a), Write(b), Write(c)]);
    }

    [Test]
    public void NoDuplicates_MatchesTreeBasedOutput()
    {
        var a = new RootEntry { Path = @"C:\data" };
        a.AddChild(File_("one.txt", 10, 1));
        a.AddChild(File_("two.txt", 11, 1));
        AssertSame([Write(a)], expectDuplicates: false);
    }

    [Test]
    public void ManySetsWithTiedSizes_SortOrderMatchesTreeBasedOutput()
    {
        // Many sets sharing the same sizes: ordering of ties comes from the (unstable) List.Sort and
        // the insertion order of the sets, both of which must be reproduced exactly.
        var a = new RootEntry { Path = @"C:\data" };
        var b = new RootEntry { Path = @"D:\data" };
        for (var set = 0; set < 60; set++)
        {
            var size = 100 + set % 4;
            var d = new DirEntry(true) { Path = $"d{set}" };
            d.AddChild(File_($"f{set}-1.txt", size, 1000 + set));
            d.AddChild(File_($"f{set}-2.txt", size, 1000 + set));
            (set % 2 == 0 ? a : b).AddChild(d);
            if (set % 3 == 0) b.AddChild(File_($"extra{set}.txt", size, 1000 + set));
        }

        AssertSame([Write(a), Write(b)]);
    }

    private static void Add(RootEntry root, DirEntry parent, DirEntry child)
    {
        if (parent == null) root.AddChild(child);
        else parent.AddChild(child);
    }

    [TestCase(1, null)]
    [TestCase(2, null)]
    [TestCase(3, null)]
    [TestCase(42, null)]
    [TestCase(7, 5)] // tiny size-pass budget forces the multi-pass size counting
    [TestCase(8, 1)]
    public void RandomCatalogs_MatchTreeBasedOutput(int seed, int? sizesPerPass)
    {
        var rnd = new Random(seed);
        var roots = new[] { @"C:\", @"C:\users", @"E:\media", @"\\nas\backup" };
        var files = new List<string>();
        foreach (var rootPath in roots)
        {
            var root = new RootEntry { Path = rootPath };
            var dirs = new List<DirEntry> { null }; // null = the root itself
            for (var i = 0; i < 400; i++)
            {
                var parent = dirs[rnd.Next(dirs.Count)];
                if (rnd.Next(5) == 0)
                {
                    var d = new DirEntry(true) { Path = $"dir{i}" };
                    Add(root, parent, d);
                    dirs.Add(d);
                }
                else
                {
                    var size = rnd.Next(4) == 0 ? 0 : 50 + rnd.Next(6);
                    var f = rnd.Next(6) == 0
                        ? File_($"fïle{i}.dat", size)
                        : File_($"fïle{i}.dat", size, rnd.Next(3), partial: rnd.Next(5) == 0);
                    Add(root, parent, f);
                }
            }

            files.Add(Write(root));
        }

        AssertSame(files, sizesPerPass: sizesPerPass);
    }

    /// <summary>
    /// Memory evidence for the columnar path. Explicit: run manually with
    /// <c>dotnet test --filter Name~MeasureManagedMemory</c> and read the test output.
    /// </summary>
    [Test, Explicit("Measurement only")]
    public void MeasureManagedMemory_TreeVersusColumnar()
    {
        const int total = 300_000;
        const int dupeEvery = 100; // 1% of files belong to a duplicate pair
        var root = new RootEntry { Path = @"C:\big" };
        var rnd = new Random(1);
        DirEntry dir = null;
        for (var i = 0; i < total; i++)
        {
            if (i % 500 == 0)
            {
                dir = new DirEntry(true) { Path = $"dir{i}" };
                root.AddChild(dir);
            }

            var dupe = i % dupeEvery < 2;
            // unique sizes for non-duplicates, so only the duplicate pairs are candidates
            var size = dupe ? 1000 + i / dupeEvery : 10_000_000L + i;
            var hash = dupe ? 100_000 + i / dupeEvery : rnd.Next();
            dir!.AddChild(File_($"file-number-{i}.dat", size, hash));
        }

        var file = Write(root);
        root = null;
        string[] files = [file];

        TestContext.Out.WriteLine($"entries: {total:N0}, duplicate candidates: {total / dupeEvery * 2:N0}");
        TestContext.Out.WriteLine("tree-based (build tree + FindDuplicates), live heap while tree is alive:");
        {
            var logger = new RecordingLogger(TextWriter.Null);
            GC.Collect(2, GCCollectionMode.Forced, true);
            var baseline = GC.GetTotalMemory(true);
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var trees = CatalogTreeBuilder.FromColumnarFiles(files);
            var live = GC.GetTotalMemory(true) - baseline;
            var original = Console.Out;
            Console.SetOut(TextWriter.Null);
            try { NewDuplication(logger).FindDuplicates(trees); }
            finally { Console.SetOut(original); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            TestContext.Out.WriteLine($"  live heap held by trees: {live:N0} bytes ({live / (double)total:F1} B/entry); total allocated: {allocated:N0}");
            GC.KeepAlive(trees);
        }

        TestContext.Out.WriteLine("columnar (readers + GetDuplicateSets):");
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            var baseline = GC.GetTotalMemory(true);
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            using var reader = new ColumnarCatalogReader(file);
            var sets = new ColumnarDuplicateFinder(new RecordingLogger(TextWriter.Null))
                .GetDuplicateSets([reader]);
            var live = GC.GetTotalMemory(true) - baseline;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
            TestContext.Out.WriteLine($"  live heap held (results kept alive): {live:N0} bytes; total allocated: {allocated:N0}; sets: {sets.Count}");
            GC.KeepAlive(sets);
        }
    }
}
