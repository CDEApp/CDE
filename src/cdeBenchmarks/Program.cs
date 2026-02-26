using BenchmarkDotNet.Running;

namespace cdeBenchmarks;

/// <summary>
/// CDE (Catalog Directory Entries) Performance Benchmarks
///
/// This benchmark suite measures performance-critical operations in the CDE cataloging system
/// which handles billions of file entries efficiently.
///
/// Usage:
///   dotnet run -c Release                          # Run all benchmarks
///   dotnet run -c Release --filter *Hash16*        # Run only Hash16 benchmarks
///   dotnet run -c Release --filter *DirEntry*      # Run only DirEntry benchmarks
///   dotnet run -c Release --filter *Pooling*       # Run only Pooling benchmarks
///
/// Key Areas Benchmarked:
///
/// 1. Hash16Benchmarks
///    - Tests the struct-based Hash16 implementation (was a class, converted to struct)
///    - Measures: Creation, equality, dictionary operations, memory allocation
///    - Impact: 240+ MB savings for 10M hashed files
///
/// 2. DirEntryBenchmarks
///    - Core entity operations called millions of times
///    - Measures: Path comparison, size sorting, date comparison, path interning
///    - Impact: Critical for search/sort performance
///
/// 3. PoolingBenchmarks
///    - Object pooling infrastructure to reduce GC pressure
///    - Measures: BufferPool (hashing), CollectionPool (directory lists), Stack pooling
///    - Impact: Reduces allocations and GC pressure with billions of entries
///
/// Performance Context (from README):
///   - 500M entries:  96 MB memory (32-bit),  22 MB file size
///   - 1.5B entries: 275 MB memory (32-bit),  65 MB file size
///   - 11B entries:  2.5 GB memory (64-bit), 500 MB file size (7 catalogs)
///
/// Tips:
///   - Always run in Release mode (-c Release)
///   - Close other applications to reduce noise
///   - Results are saved to BenchmarkDotNet.Artifacts/
///   - Use MemoryDiagnoser results to verify allocation improvements
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

        // Alternative: Run specific benchmark classes
        // BenchmarkRunner.Run<Hash16Benchmarks>(args);
        // BenchmarkRunner.Run<DirEntryBenchmarks>(args);
        // BenchmarkRunner.Run<PoolingBenchmarks>(args);
    }
}
