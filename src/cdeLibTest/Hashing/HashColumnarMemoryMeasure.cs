using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #40 memory evidence: peak managed heap while <c>cde hash</c> runs, for the tree path (rebuild trees,
/// hash, write) and for the columnar path (<see cref="ColumnarCatalogHashing.HashAndSave"/>). The catalogs list
/// files that do not exist, so every hash attempt fails fast and the numbers show the bookkeeping, not file I/O.
/// Explicit: run with <c>dotnet test --filter Name~Measure_HashPeakHeap</c> and read the test output.
/// </summary>
[TestFixture]
[Explicit("Measurement, not a regression test")]
[NonParallelizable]
public class HashColumnarMemoryMeasure
{
    private sealed class NullLogger : ILogger
    {
        public void LogException(Exception ex, string format, params object[] paramList) { }
        public void LogInfo(string format, params object[] paramList) { }
        public void LogDebug(string format, params object[] paramList) { }
    }

    private static Duplication NewDuplication()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(5000);
        config.HashFirstPassSize.Returns(65536);
        return new Duplication(new NullLogger(), config, Substitute.For<IApplicationDiagnostics>(), new OperationCancellation());
    }

    /// <summary>
    /// A catalog of <paramref name="entries"/> files, <paramref name="candidates"/> of them in same-size pairs
    /// (half the pairs already carry an equal partial hash, so the full-hash phase has work too); every other
    /// file has a unique size.
    /// </summary>
    private static string Generate(int entries, int candidates)
    {
        var root = new RootEntry { Path = Path.Combine(Path.GetTempPath(), "cde-missing-" + Guid.NewGuid().ToString("N")) };
        DirEntry dir = null;
        for (var i = 0; i < entries; i++)
        {
            if (i % 500 == 0)
            {
                dir = new DirEntry(true) { Path = $"dir{i}" };
                root.AddChild(dir);
            }

            var file = new DirEntry(false) { Path = $"file-number-{i}.dat" };
            if (i < candidates)
            {
                var pair = i / 2;
                file.Size = 1_000_000 + pair;
                if (pair % 2 == 0)
                {
                    file.SetHash((ulong)pair + 1);
                    file.IsPartialHash = true;
                }
            }
            else
            {
                file.Size = 100_000_000L + i;
            }

            dir!.AddChild(file);
        }

        root.SetInMemoryFields();
        var path = Path.Combine(Path.GetTempPath(), $"cdehashmeasure-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    /// <summary>Peak GC heap above the starting heap, seen by a 1ms poller while <paramref name="a"/> runs.</summary>
    private static long PeakHeapGrowth(Action a)
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var baseline = GC.GetTotalMemory(true);
        long peak = baseline;
        var stop = false;
        var t = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                Interlocked.Exchange(ref peak, Math.Max(Interlocked.Read(ref peak), GC.GetTotalMemory(false)));
                Thread.Sleep(1);
            }
        }) { IsBackground = true };
        t.Start();
        a();
        Volatile.Write(ref stop, true);
        t.Join();
        return Math.Max(peak, GC.GetTotalMemory(false)) - baseline;
    }

    private static long Columnar(string file) => PeakHeapGrowth(() =>
        ColumnarCatalogHashing.HashAndSave(NewDuplication(), [file], null).GetAwaiter().GetResult());

    private static long Tree(string file) => PeakHeapGrowth(() =>
    {
        var trees = CatalogTreeBuilder.FromColumnarFiles([file]);
        NewDuplication().ApplyHash(trees).GetAwaiter().GetResult();
        ColumnarFormat.Write(EntryStore.Build(trees[0]), file);
    });

    [Test]
    public void Measure_HashPeakHeap_TreeVersusColumnar()
    {
        var files = new List<string>();
        try
        {
            string Make(int entries, int candidates)
            {
                var f = Generate(entries, candidates);
                files.Add(f);
                return f;
            }

            var small = Make(600_000, 6_000);
            var big = Make(1_200_000, 6_000);
            var manyCandidates = Make(600_000, 60_000);
            var treeCopy = Make(600_000, 6_000);

            var c600 = Columnar(small);
            var c1200 = Columnar(big);
            var c600Many = Columnar(manyCandidates);
            var t600 = Tree(treeCopy);

            static string Mb(long b) => $"{b / (1024.0 * 1024.0),8:F1} MB";
            TestContext.Out.WriteLine("peak managed heap growth during cde hash:");
            TestContext.Out.WriteLine($"  tree     600k entries,  6k candidates: {Mb(t600)}");
            TestContext.Out.WriteLine($"  columnar 600k entries,  6k candidates: {Mb(c600)}");
            TestContext.Out.WriteLine($"  columnar 1.2M entries,  6k candidates: {Mb(c1200)}");
            TestContext.Out.WriteLine($"  columnar 600k entries, 60k candidates: {Mb(c600Many)}");

            Assert.That(c600, Is.LessThan(t600 / 4), "the columnar path holds no tree");
        }
        finally
        {
            foreach (var f in files)
            {
                try { File.Delete(f); } catch (IOException) { /* ignore */ }
            }
        }
    }
}
