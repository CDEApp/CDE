using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;

namespace cdeMemProbe;

/// <summary>
/// <c>--rescan-phases &lt;dir&gt; [--runs N] [--threads N] [--work dir]</c> (#235 Step 0). Runs the same steps as
/// <c>CatalogSession.RescanCore</c> against a real tree and prints the time each phase takes: enumerate
/// (<c>PopulateRoot</c>), carry hashes forward, sort and summarise, <c>WriteDirect</c>, and swap (rename and re-map).
/// The first run writes the catalog the later runs rescan over, so hash carry-forward reads a real old catalog.
/// </summary>
public static class RescanPhases
{
    public static int Run(string root, Func<string, string?> flag)
    {
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"--rescan-phases: no such directory {root}");
            return 1;
        }

        var runs = int.TryParse(flag("--runs"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) && r > 0 ? r : 3;
        var threads = int.TryParse(flag("--threads"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t > 0 ? t : 1;
        var work = flag("--work") ?? Path.Combine(Path.GetTempPath(), "cde-rescan-phases");
        Directory.CreateDirectory(work);
        var target = Path.Combine(work, "probe.cdex");
        File.Delete(target);

        Console.WriteLine("run,entries,enumerateMs,carryHashesMs,sortSummaryMs,writeMs,swapMs,totalMs,cdexBytes,peakWorkingSet,enumerateGcPauseMs");
        for (var i = 0; i <= runs; i++)
        {
            var line = RunOnce(root, target, threads, i == 0 ? "seed" : i.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(line);
        }

        return 0;
    }

    private static string RunOnce(string root, string target, int threads, string label)
    {
        var tmp = target + ".tmp";
        var gcBefore = GC.GetTotalPauseDuration();
        var sw = Stopwatch.StartNew();

        var tree = new RootEntry();
        tree.PopulateRoot(root, threads: threads);
        var enumerate = sw.Elapsed;
        var enumerateGc = GC.GetTotalPauseDuration() - gcBefore;

        if (File.Exists(target))
        {
            using var old = new ColumnarCatalogReader(target);
            ColumnarHashReuse.CopyHashes(old, tree);
        }
        var carry = sw.Elapsed;

        tree.SortAllChildrenByPath();
        tree.SetSummaryFields();
        var sort = sw.Elapsed;

        ColumnarFormat.WriteDirect(tree, tmp);
        var write = sw.Elapsed;

        var aside = target + ".old";
        if (File.Exists(target)) File.Move(target, aside, overwrite: true);
        File.Move(tmp, target);
        using (new ColumnarCatalogReader(target)) { }
        File.Delete(aside);
        var swap = sw.Elapsed;

        var entries = (long)tree.FileEntryCount + tree.DirEntryCount;
        Process.GetCurrentProcess().Refresh();
        return string.Join(',', label, entries,
            Ms(enumerate), Ms(carry - enumerate), Ms(sort - carry), Ms(write - sort), Ms(swap - write), Ms(swap),
            new FileInfo(target).Length, Process.GetCurrentProcess().PeakWorkingSet64, Ms(enumerateGc));
    }

    private static string Ms(TimeSpan t) => t.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
}
