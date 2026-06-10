using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
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

        if (HasFlag(args, "--soa", out _))
        {
            return MeasureSoa(args);
        }

        if (HasFlag(args, "--migrate", out var migrateIn))
        {
            return Migrate(args, migrateIn, logger);
        }

        if (HasFlag(args, "--flat", out var flatFile))
        {
            return MeasureFlat(args, flatFile);
        }

        if (HasFlag(args, "--generate", out var genValue))
        {
            return await GenerateAsync(args, genValue, logger);
        }

        return await MeasureAsync(args[0], !HasFlag(args, "--no-header", out _),
            asStore: HasFlag(args, "--store", out _), logger);
    }

    private static async Task<int> GenerateAsync(string[] args, string? countArg, ILogger logger)
    {
        if (!int.TryParse(countArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            Console.Error.WriteLine("--generate requires a positive entry count, e.g. --generate 1000000");
            return 1;
        }

        var withHashes = HasFlag(args, "--hashes", out _);
        var sharedNames = HasFlag(args, "--shared-names", out _);
        var seed = HasFlag(args, "--seed", out var seedArg) && int.TryParse(seedArg, out var s) ? s : 42;
        var outPath = HasFlag(args, "--out", out var outArg) && !string.IsNullOrWhiteSpace(outArg)
            ? outArg!
            : Path.Combine(Path.GetTempPath(), $"cde-synthetic-{count}{(withHashes ? "-hashed" : "")}.cde");

        Console.Error.WriteLine($"Generating ~{count:N0} entries (hashes={withHashes}, seed={seed}) ...");
        var sw = Stopwatch.StartNew();
        var root = SyntheticCatalog.Generate(count, withHashes, seed, sharedNames: sharedNames);
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

    /// <summary>
    /// Measure the retained footprint of the PROTOTYPE struct-of-arrays EntryStore, for comparison
    /// with the pointer-tree model. Usage: cdeMemProbe --soa --generate N [--shared-names] [--hashes]
    /// </summary>
    private static int MeasureSoa(string[] args)
    {
        if (!HasFlag(args, "--generate", out var countArg)
            || !int.TryParse(countArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || count <= 0)
        {
            Console.Error.WriteLine("--soa requires --generate <N>, e.g. --soa --generate 1000000");
            return 1;
        }

        var withHashes = HasFlag(args, "--hashes", out _);
        var sharedNames = HasFlag(args, "--shared-names", out _);

        Console.Error.WriteLine(
            $"Building SoA EntryStore for ~{count:N0} entries (hashes={withHashes}, sharedNames={sharedNames}) ...");

        var store = BuildStoreReleasingTree(count, withHashes, sharedNames);

        // Settle so only the live EntryStore (not the now-dead source tree) is counted.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var managed = GC.GetTotalMemory(true);
        GC.KeepAlive(store);
        var bytesPerEntry = store.Count > 0 ? (double)managed / store.Count : 0;

        // Sanity-check the SoA actually works as a searchable structure.
        var matches = 0;
        EntryStoreSearch.Find(store, sharedNames ? "x" : ".txt",
            regexMode: false, includePath: false, includeFiles: true, includeFolders: true, _ => matches++);

        Console.WriteLine("model,entries,managedBytes,bytesPerEntry");
        Console.WriteLine(string.Join(',', "soa",
            store.Count.ToString(CultureInfo.InvariantCulture),
            managed.ToString(CultureInfo.InvariantCulture),
            bytesPerEntry.ToString("F2", CultureInfo.InvariantCulture)));
        Console.Error.WriteLine($"sanity: {matches:N0} name matches; full path[1] = {store.FullPath(1)}");
        return 0;
    }

    // Separate non-inlined method so the source tree local is out of scope (collectable) before we
    // measure the store in the caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EntryStore BuildStoreReleasingTree(int count, bool withHashes, bool sharedNames)
    {
        var tree = SyntheticCatalog.Generate(count, withHashes, seed: 42, sharedNames: sharedNames);
        return EntryStore.Build(tree);
    }

    private static async Task<int> MeasureAsync(string file, bool printHeader, bool asStore, ILogger logger)
    {
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"catalog not found: {file}");
            return 1;
        }

        var sw = Stopwatch.StartNew();
        // Load (and for --store, convert to the SoA store) inside a synchronous helper so the source
        // tree is a plain local that goes fully out of scope before we measure. (An async helper would
        // capture the tree in its state machine and the store measurement would double-count it.)
        var (measured, entries) = asStore
            ? LoadAsStore(file, logger)
            : LoadAsTree(file, logger);
        sw.Stop();

        if (measured == null)
        {
            Console.Error.WriteLine($"failed to load catalog: {file}");
            return 1;
        }

        // Settle the GC so GetTotalMemory reflects retained (live) objects, not transient load garbage.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var managedBytes = GC.GetTotalMemory(true);
        using var proc = Process.GetCurrentProcess();
        var peakWorkingSet = proc.PeakWorkingSet64;
        var privateBytes = proc.PrivateMemorySize64;

        // Keep the measured object (tree or store) alive across the measurement.
        GC.KeepAlive(measured);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Measured, long Entries) LoadAsTree(string file, ILogger logger)
    {
        using var repo = new CatalogRepository(logger);
        var root = repo.LoadDirCache(file);
        if (root == null) return (null!, 0);
        return (root, root.FileEntryCount + root.DirEntryCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Measured, long Entries) LoadAsStore(string file, ILogger logger)
    {
        using var repo = new CatalogRepository(logger);
        var root = repo.LoadDirCache(file);
        if (root == null) return (null!, 0);
        var entries = root.FileEntryCount + root.DirEntryCount;
        var store = EntryStore.Build(root);
        // root is a plain local; once this returns it is unreferenced and collectable, leaving only
        // the store (which reuses the tree's interned name strings) for the caller to measure.
        return (store, entries);
    }

    /// <summary>
    /// One-way migration: load an existing MessagePack .cde, convert to the SoA EntryStore, and write
    /// the columnar/mmap format. Usage: cdeMemProbe --migrate &lt;in.cde&gt; [--out &lt;out.cdex&gt;]
    /// </summary>
    private static int Migrate(string[] args, string? inFile, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(inFile) || !File.Exists(inFile))
        {
            Console.Error.WriteLine("--migrate requires an existing <in.cde>, e.g. --migrate cat.cde --out cat.cdex");
            return 1;
        }

        var outFile = HasFlag(args, "--out", out var outArg) && !string.IsNullOrWhiteSpace(outArg)
            ? outArg!
            : Path.ChangeExtension(inFile, ".cdex");

        var sw = Stopwatch.StartNew();
        EntryStore store;
        using (var repo = new CatalogRepository(logger))
        {
            var root = repo.LoadDirCache(inFile);
            if (root == null)
            {
                Console.Error.WriteLine($"failed to load catalog: {inFile}");
                return 1;
            }
            store = EntryStore.Build(root);
        }
        ColumnarFormat.Write(store, outFile);
        sw.Stop();

        var srcLen = new FileInfo(inFile).Length;
        var dstLen = new FileInfo(outFile).Length;
        Console.Error.WriteLine(
            $"migrated {store.Count:N0} entries: {Path.GetFileName(inFile)} ({srcLen:N0} B) -> " +
            $"{Path.GetFileName(outFile)} ({dstLen:N0} B) in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine(outFile);
        return 0;
    }

    /// <summary>
    /// Measure the zero-copy mmap path: open the columnar file (no managed load), run a search, and
    /// report retained heap, allocations DURING the search, working set, and timing.
    /// Usage: cdeMemProbe --flat &lt;file.cdex&gt; [--pattern X] [--path]
    /// </summary>
    private static int MeasureFlat(string[] args, string? flatFile)
    {
        if (string.IsNullOrWhiteSpace(flatFile) || !File.Exists(flatFile))
        {
            Console.Error.WriteLine("--flat requires an existing <file.cdex>");
            return 1;
        }

        var pattern = HasFlag(args, "--pattern", out var p) && !string.IsNullOrEmpty(p) ? p! : ".txt";
        var pathMode = HasFlag(args, "--path", out _);

        // Settle, then snapshot allocation + heap baselines so we can isolate the search's own cost.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var heapBefore = GC.GetTotalMemory(true);

        var openSw = Stopwatch.StartNew();
        using var reader = new ColumnarCatalogReader(flatFile);
        openSw.Stop();

        var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        var searchSw = Stopwatch.StartNew();
        var matches = pathMode
            ? reader.FindPath(pattern, includeFiles: true, includeFolders: true)
            : reader.FindName(pattern, includeFiles: true, includeFolders: true);
        searchSw.Stop();
        var allocDuringSearch = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;

        var heapAfter = GC.GetTotalMemory(false); // no forced collect: show what the search left live
        using var proc = Process.GetCurrentProcess();
        var workingSet = proc.WorkingSet64;
        var fileBytes = new FileInfo(flatFile).Length;
        GC.KeepAlive(reader);

        var heapPerEntry = reader.Count > 0 ? (double)heapAfter / reader.Count : 0;

        Console.WriteLine(
            "file,entries,mode,openMs,searchMs,matches,allocDuringSearch,heapBytes,heapPerEntry,workingSet,fileBytes");
        Console.WriteLine(string.Join(',',
            Path.GetFileName(flatFile),
            reader.Count.ToString(CultureInfo.InvariantCulture),
            pathMode ? "path" : "name",
            openSw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
            searchSw.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture),
            matches.ToString(CultureInfo.InvariantCulture),
            allocDuringSearch.ToString(CultureInfo.InvariantCulture),
            heapAfter.ToString(CultureInfo.InvariantCulture),
            heapPerEntry.ToString("F2", CultureInfo.InvariantCulture),
            workingSet.ToString(CultureInfo.InvariantCulture),
            fileBytes.ToString(CultureInfo.InvariantCulture)));
        Console.Error.WriteLine(
            $"baseline heap before open: {heapBefore:N0} B; sample path[1] = {reader.FullPath(1)}");
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
