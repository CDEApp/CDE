using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using Serilog;

namespace cdeMemProbe;

/// <summary>
/// Standalone footprint probe for cde catalogs. Measures the RETAINED managed heap and process
/// working set after a catalog is fully loaded — the headline metric for the memory-reduction
/// effort. BenchmarkDotNet's MemoryDiagnoser measures allocations during a run, not steady-state
/// retained size, so this lives in its own minimal process.
///
/// Usage:
///   cdeMemProbe --generate &lt;N&gt; [--hashes] [--out &lt;path.cde&gt;] [--seed N]
///       Generate a synthetic catalog of ~N entries and save it. Prints the path. No measurement.
///
///   cdeMemProbe &lt;path.cde&gt; [--no-header]
///       Load the catalog, settle the GC, and emit one CSV row of footprint metrics.
///
/// Typical flow (clean measurement = generate and measure in separate processes):
///   cdeMemProbe --generate 1000000 --out fixture-1m.cde
///   cdeMemProbe fixture-1m.cde
/// </summary>
public static class Program
{
    private const string CsvHeader =
        "file,entries,loadMs,managedBytes,peakWorkingSet,privateBytes,bytesPerEntry";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "usage: cdeMemProbe --generate <N> [--hashes] [--out <path>] [--seed N]\n" +
                "       cdeMemProbe <path.cde> [--no-header]");
            return 1;
        }

        // Silent Serilog logger (no sinks) so CatalogRepository stays quiet and out of the CSV.
        var logger = new LoggerConfiguration().CreateLogger();

        if (HasFlag(args, "--generate", out var genValue))
        {
            return await GenerateAsync(args, genValue, logger);
        }

        return await MeasureAsync(args[0], !HasFlag(args, "--no-header", out _), logger);
    }

    private static async Task<int> GenerateAsync(string[] args, string? countArg, ILogger logger)
    {
        if (!int.TryParse(countArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            Console.Error.WriteLine("--generate requires a positive entry count, e.g. --generate 1000000");
            return 1;
        }

        var withHashes = HasFlag(args, "--hashes", out _);
        var seed = HasFlag(args, "--seed", out var seedArg) && int.TryParse(seedArg, out var s) ? s : 42;
        var outPath = HasFlag(args, "--out", out var outArg) && !string.IsNullOrWhiteSpace(outArg)
            ? outArg!
            : Path.Combine(Path.GetTempPath(), $"cde-synthetic-{count}{(withHashes ? "-hashed" : "")}.cde");

        Console.Error.WriteLine($"Generating ~{count:N0} entries (hashes={withHashes}, seed={seed}) ...");
        var sw = Stopwatch.StartNew();
        var root = SyntheticCatalog.Generate(count, withHashes, seed);
        root.ActualFileName = outPath;
        using (var repo = new CatalogRepository(logger))
        {
            await repo.Save(root);
        }
        sw.Stop();

        var actual = root.FileEntryCount + root.DirEntryCount;
        var fileInfo = new FileInfo(outPath);
        Console.Error.WriteLine(
            $"Wrote {actual:N0} entries to {outPath} ({fileInfo.Length:N0} bytes on disk) in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine(outPath);
        return 0;
    }

    private static async Task<int> MeasureAsync(string file, bool printHeader, ILogger logger)
    {
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"catalog not found: {file}");
            return 1;
        }

        var sw = Stopwatch.StartNew();
        RootEntry root;
        using (var repo = new CatalogRepository(logger))
        {
            root = await repo.LoadDirCacheAsync(file);
        }
        sw.Stop();

        if (root == null)
        {
            Console.Error.WriteLine($"failed to load catalog: {file}");
            return 1;
        }

        var entries = root.FileEntryCount + root.DirEntryCount;

        // Settle the GC so GetTotalMemory reflects retained (live) objects, not transient load garbage.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var managedBytes = GC.GetTotalMemory(true);
        using var proc = Process.GetCurrentProcess();
        var peakWorkingSet = proc.PeakWorkingSet64;
        var privateBytes = proc.PrivateMemorySize64;

        // Keep the tree alive across the measurement so it counts toward the live heap.
        GC.KeepAlive(root);

        var bytesPerEntry = entries > 0 ? (double)managedBytes / entries : 0;

        if (printHeader)
        {
            Console.WriteLine(CsvHeader);
        }

        Console.WriteLine(string.Join(',',
            Path.GetFileName(file),
            entries.ToString(CultureInfo.InvariantCulture),
            sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
            managedBytes.ToString(CultureInfo.InvariantCulture),
            peakWorkingSet.ToString(CultureInfo.InvariantCulture),
            privateBytes.ToString(CultureInfo.InvariantCulture),
            bytesPerEntry.ToString("F2", CultureInfo.InvariantCulture)));
        return 0;
    }

    /// <summary>
    /// Returns true if <paramref name="name"/> is present. If the next token is not another flag it
    /// is returned as <paramref name="value"/> (so both <c>--generate 100</c> and bare flags work).
    /// </summary>
    private static bool HasFlag(string[] args, string name, out string? value)
    {
        value = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
            }
            return true;
        }
        return false;
    }
}
