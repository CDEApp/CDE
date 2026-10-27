using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// Allocation / memory evidence for the scan pipeline (scan, hash reuse, save): the legacy code path
/// versus the current one, on a generated tree. Explicit: run with
/// <c>dotnet test --filter Name=Measure_ScanPipeline</c>; results land in %TEMP%\cde-measure.txt.
/// </summary>
[TestFixture]
[Explicit("Measurement, not a regression test")]
public class ScanPipelineMeasure
{
    private const int Dirs = 500;
    private const int FilesPerDir = 200;

    private readonly StringBuilder _report = new();

    private void Say(string line)
    {
        _report.AppendLine(line);
        Console.WriteLine(line);
    }

    // Verbatim copy of the pre-change enumeration (FileSystemInfo per entry, string.Intern'd names).
    private static void LegacyScan(RootEntry re, string root)
    {
        var stack = new Stack<(ICommonEntry, string)>();
        stack.Push((re, root));
        while (stack.Count > 0)
        {
            var (parent, directory) = stack.Pop();
            foreach (var fsInfo in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                var d = new DirEntry(fsInfo);
                parent.AddChild(d);
                if (d.IsDirectory && !d.IsReparsePoint) stack.Push((d, fsInfo.FullName));
            }
        }
    }

    private static long Allocated(Action a)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        a();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static long Retained() => GC.GetTotalMemory(true);

    /// <summary>Peak GC heap size seen by a 1ms poller while <paramref name="a"/> runs (includes garbage not yet collected).</summary>
    private static long PeakHeap(Action a)
    {
        GC.Collect();
        long peak = 0;
        var stop = false;
        var t = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                peak = Math.Max(peak, GC.GetTotalMemory(false));
                Thread.Sleep(1);
            }
        })
        { IsBackground = true };
        t.Start();
        a();
        peak = Math.Max(peak, GC.GetTotalMemory(false));
        Volatile.Write(ref stop, true);
        t.Join();
        return peak;
    }

    [Test]
    public void Measure_ScanPipeline()
    {
        var root = Path.Combine(Path.GetTempPath(), "cdemeasure-" + Guid.NewGuid().ToString("N"));
        var work = Path.Combine(Path.GetTempPath(), "cdemeasure-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            for (var d = 0; d < Dirs; d++)
            {
                var dir = Path.Combine(root, "directory_" + d % 50, "sub_" + d);
                Directory.CreateDirectory(dir);
                for (var f = 0; f < FilesPerDir; f++)
                    File.WriteAllText(Path.Combine(dir, $"file_{f}.txt"), "x");
            }

            var entries = Dirs * FilesPerDir + Dirs + 50;
            Say($"tree: {entries:N0} entries");

            // ---- #41 scan ----
            for (var warm = 0; warm < 2; warm++)
            {
                var w1 = new RootEntry(); LegacyScan(w1, root);
                var w2 = new RootEntry(); w2.RecurseTree(root);
            }

            var legacy = new RootEntry { Path = root };
            var legacyBytes = Allocated(() => LegacyScan(legacy, root));
            var current = new RootEntry { Path = root };
            var currentBytes = Allocated(() => current.RecurseTree(root));
            Say($"#41 scan allocated  legacy {legacyBytes:N0} B ({legacyBytes / (double)entries:N0} B/entry)  " +
                $"current {currentBytes:N0} B ({currentBytes / (double)entries:N0} B/entry)");
            legacy = null;

            // ---- #42 hash reuse ----
            var full = new RootEntry();
            full.PopulateRoot(root);
            var hashSeed = 1;
            void SeedHashes(ICommonEntry p)
            {
                foreach (var c in p.Children)
                {
                    if (c.IsDirectory) SeedHashes(c);
                    else
                    {
                        var b = new byte[16];
                        BitConverter.TryWriteBytes(b, hashSeed++);
                        c.SetHash(b);
                    }
                }
            }
            SeedHashes(full);
            full.SortAllChildrenByPath();
            full.SetSummaryFields();
            var oldCdex = Path.Combine(work, "old.cdex");
            ColumnarFormat.Write(full, oldCdex);
            full = null;

            long legacyReuse, currentReuse, legacyPeak, currentPeak, oldTreeRetained;
            {
                var fresh = new RootEntry(); fresh.PopulateRoot(root);
                var baseline = Retained();
                RootEntry oldRoot;
                using (var r = new ColumnarCatalogReader(oldCdex)) oldRoot = CatalogTreeBuilder.FromSource(r);
                oldTreeRetained = Retained() - baseline;
                GC.KeepAlive(oldRoot);
                GC.KeepAlive(fresh);
            }
            {
                var fresh = new RootEntry(); fresh.PopulateRoot(root);
                legacyReuse = Allocated(() =>
                {
                    RootEntry oldRoot;
                    using (var r = new ColumnarCatalogReader(oldCdex)) oldRoot = CatalogTreeBuilder.FromSource(r);
                    oldRoot.TraverseTreesCopyHash(fresh);
                });
            }
            {
                var fresh = new RootEntry(); fresh.PopulateRoot(root);
                currentReuse = Allocated(() =>
                {
                    using var r = new ColumnarCatalogReader(oldCdex);
                    ColumnarHashReuse.CopyHashes(r, fresh);
                });
                var fresh2 = new RootEntry(); fresh2.PopulateRoot(root);
                currentPeak = PeakHeap(() =>
                {
                    using var r = new ColumnarCatalogReader(oldCdex);
                    ColumnarHashReuse.CopyHashes(r, fresh2);
                });
            }
            {
                var fresh3 = new RootEntry(); fresh3.PopulateRoot(root);
                legacyPeak = PeakHeap(() =>
                {
                    RootEntry oldRoot;
                    using (var r = new ColumnarCatalogReader(oldCdex)) oldRoot = CatalogTreeBuilder.FromSource(r);
                    oldRoot.TraverseTreesCopyHash(fresh3);
                });
            }
            Say($"#42 hash reuse allocated  legacy {legacyReuse:N0} B  current {currentReuse:N0} B");
            Say($"#42 old catalog pointer tree retained while reusing: {oldTreeRetained:N0} B (current path builds none)");
            Say($"#42 peak GC heap during reuse (1ms poll, includes uncollected garbage)  legacy {legacyPeak:N0} B  current {currentPeak:N0} B");

            // ---- #43 save ----
            var saveTree = new RootEntry(); saveTree.PopulateRoot(root);
            saveTree.SortAllChildrenByPath(); saveTree.SetSummaryFields();
            var outA = Path.Combine(work, "a.cdex");
            var outB = Path.Combine(work, "b.cdex");
            ColumnarFormat.Write(EntryStore.Build(saveTree), outA); // warm
            ColumnarFormat.Write(saveTree, outB);
            var legacySave = Allocated(() => ColumnarFormat.Write(EntryStore.Build(saveTree), outA));
            var currentSave = Allocated(() => ColumnarFormat.Write(saveTree, outB));
            var baseHeap = Retained();
            var store = EntryStore.Build(saveTree);
            var storeRetained = Retained() - baseHeap;
            GC.KeepAlive(store);
            store = null;
            var legacySavePeak = PeakHeap(() => ColumnarFormat.Write(EntryStore.Build(saveTree), outA));
            var currentSavePeak = PeakHeap(() => ColumnarFormat.Write(saveTree, outB));
            Say($"#43 save allocated  legacy {legacySave:N0} B  current {currentSave:N0} B");
            Say($"#43 EntryStore copy retained during legacy save: {storeRetained:N0} B");
            Say($"#43 peak GC heap during save (1ms poll, includes uncollected garbage)  legacy {legacySavePeak:N0} B  current {currentSavePeak:N0} B");
            var identical = File.ReadAllBytes(outA).AsSpan().SequenceEqual(File.ReadAllBytes(outB));
            Say($"#43 outputs identical: {identical}");
            Assert.That(identical, Is.True, "legacy and current save paths must write identical bytes");
            GC.KeepAlive(saveTree);
        }
        finally
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "cde-measure.txt"), _report.ToString());
            try { Directory.Delete(root, true); } catch { /* best effort */ }
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }
}
