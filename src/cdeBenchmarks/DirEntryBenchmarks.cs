using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using cdeLib.Entities;

namespace cdeBenchmarks;

/// <summary>
/// Benchmarks for DirEntry operations that are called millions of times during catalog operations.
/// DirEntry is the core entity representing files and directories in the catalog.
///
/// Key scenarios benchmarked:
/// 1. Path comparison (used in sorting and searching)
/// 2. Size-based sorting with directory handling
/// 3. Modified date comparisons
/// 4. Path property setter (with string interning)
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob]  // Let BenchmarkDotNet auto-detect runtime (.NET 10)
public class DirEntryBenchmarks
{
    private DirEntry[] _fileEntries = null!;
    private DirEntry[] _mixedEntries = null!;
    private string[] _testPaths = null!;

    [Params(1000, 10000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Create realistic file entries
        _fileEntries = new DirEntry[EntryCount];
        _mixedEntries = new DirEntry[EntryCount];
        _testPaths = new string[EntryCount];

        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < EntryCount; i++)
        {
            // Create file entries with varied sizes
            _fileEntries[i] = new DirEntry(false)
            {
                Size = random.Next(1000, 10_000_000),
                Modified = DateTime.Now.AddDays(-random.Next(0, 365))
            };
            _fileEntries[i].SetPath($"TestFile_{i}.txt");

            // Create mixed entries (files and directories)
            bool isDirectory = i % 3 == 0;
            _mixedEntries[i] = new DirEntry(isDirectory)
            {
                Size = isDirectory ? 0 : random.Next(1000, 10_000_000),
                Modified = DateTime.Now.AddDays(-random.Next(0, 365))
            };
            _mixedEntries[i].SetPath(isDirectory ? $"Directory_{i}" : $"File_{i}.dat");

            // Create varied paths for testing path setter performance
            _testPaths[i] = GenerateRealisticPath(i);
        }
    }

    /// <summary>
    /// Benchmark: PathCompareTo operation.
    /// This is used extensively during sorting operations when displaying results.
    ///
    /// Expected: Span-based comparison should be very fast with minimal allocations.
    /// </summary>
    [Benchmark(Description = "Path comparison")]
    public int PathComparison()
    {
        int result = 0;
        for (int i = 0; i < _fileEntries.Length - 1; i++)
        {
            result += _fileEntries[i].PathCompareTo(_fileEntries[i + 1]);
        }
        return result;
    }

    /// <summary>
    /// Benchmark: PathCompareWithDirTo operation.
    /// This ensures directories appear before files in sorted lists.
    ///
    /// Expected: Branch prediction should work well for sorted data.
    /// </summary>
    [Benchmark(Description = "Path comparison with directory priority")]
    public int PathComparisonWithDirectory()
    {
        int result = 0;
        for (int i = 0; i < _mixedEntries.Length - 1; i++)
        {
            result += _mixedEntries[i].PathCompareWithDirTo(_mixedEntries[i + 1]);
        }
        return result;
    }

    /// <summary>
    /// Benchmark: SizeCompareWithDirTo operation.
    /// This is critical for finding large files and sorting by size.
    ///
    /// Expected: Size comparison is fast but includes directory handling logic.
    /// </summary>
    [Benchmark(Description = "Size comparison with directory handling")]
    public int SizeComparison()
    {
        int result = 0;
        for (int i = 0; i < _mixedEntries.Length - 1; i++)
        {
            result += _mixedEntries[i].SizeCompareWithDirTo(_mixedEntries[i + 1]);
        }
        return result;
    }

    /// <summary>
    /// Benchmark: ModifiedCompareTo operation.
    /// Used for sorting by modification date.
    ///
    /// Expected: DateTime comparison is straightforward but includes bad date handling.
    /// </summary>
    [Benchmark(Description = "Modified date comparison")]
    public int ModifiedComparison()
    {
        int result = 0;
        for (int i = 0; i < _fileEntries.Length - 1; i++)
        {
            result += _fileEntries[i].ModifiedCompareTo(_fileEntries[i + 1]);
        }
        return result;
    }

    /// <summary>
    /// Benchmark: Path property setter with string interning.
    /// This is called for every file entry during catalog creation.
    ///
    /// Expected: String interning adds overhead but saves memory for duplicate strings.
    /// The benchmark measures the cost vs benefit trade-off.
    /// </summary>
    [Benchmark(Description = "Path setter with interning")]
    public void PathSetterWithInterning()
    {
        var entry = new DirEntry(false);
        foreach (var path in _testPaths)
        {
            entry.SetPath(path);
        }
    }

    /// <summary>
    /// Benchmark: Full sort operation on entries.
    /// This simulates real-world sorting during search result display.
    ///
    /// Expected: Demonstrates overall comparison performance under realistic conditions.
    /// </summary>
    [Benchmark(Description = "Sort entries by size descending")]
    public DirEntry[] SortBySize()
    {
        var entries = (DirEntry[])_mixedEntries.Clone();
        Array.Sort(entries, (a, b) => -a.SizeCompareWithDirTo(b)); // Descending
        return entries;
    }

    /// <summary>
    /// Benchmark: Creating DirEntry instances.
    /// Measures the baseline cost of instantiation including Children list pooling.
    ///
    /// Expected: Directory creation includes collection pool overhead.
    /// </summary>
    [Benchmark(Description = "Create DirEntry instances")]
    public DirEntry CreateDirEntries()
    {
        DirEntry? last = null;
        for (int i = 0; i < 100; i++)
        {
            // Alternate between files and directories
            last = new DirEntry(i % 2 == 0);
            last.SetPath($"Entry_{i}");
            last.Size = i * 1000;
        }
        return last!;
    }

    /// <summary>
    /// Helper to generate realistic file paths with extensions
    /// </summary>
    private static string GenerateRealisticPath(int index)
    {
        string[] extensions = { ".txt", ".jpg", ".pdf", ".doc", ".mp4", ".zip", ".exe", ".dll" };
        string[] prefixes = { "Document", "Image", "Video", "Archive", "Config", "Data", "Log", "Report" };

        var ext = extensions[index % extensions.Length];
        var prefix = prefixes[index % prefixes.Length];

        return $"{prefix}_{index:D6}{ext}";
    }
}
