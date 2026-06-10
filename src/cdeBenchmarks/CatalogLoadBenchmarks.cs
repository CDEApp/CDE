using BenchmarkDotNet.Attributes;
using cdeLib.Entities;

namespace cdeBenchmarks;

/// <summary>
/// End-to-end catalog LOAD benchmark: deserialize a real on-disk <c>.cde</c> through the production
/// load path (<c>CatalogRepository.LoadDirCacheAsync</c> → MessagePack → <c>SetInMemoryFields</c>).
///
/// Measures load wall-clock and (via MemoryDiagnoser) allocations during load. Retained footprint
/// is measured separately by the standalone <c>cdeMemProbe</c> — BenchmarkDotNet cannot report
/// steady-state retained heap, only per-iteration allocations.
///
///   dotnet run -c Release --filter *CatalogLoad*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)] // Load is expensive; keep iteration count modest.
public class CatalogLoadBenchmarks
{
    private string _catalogPath = null!;

    // 10M takes real time/disk; start at 1M for routine runs and opt into 10M explicitly via filter.
    [Params(1_000_000)]
    public int EntryCount { get; set; }

    [Params(false, true)]
    public bool WithHashes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _catalogPath = CatalogFixture.WriteTemp(EntryCount, WithHashes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CatalogFixture.TryDelete(_catalogPath);
    }

    [Benchmark(Description = "Load catalog from disk")]
    public RootEntry LoadCatalog()
    {
        return CatalogFixture.Load(_catalogPath);
    }
}
