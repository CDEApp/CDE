using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using cdeLib;
using cdeLib.Entities;
using cdeMemProbe;

namespace cdeBenchmarks;

/// <summary>
/// Sync (production) vs legacy async find across DIFFERENT catalog counts at a fixed ~1M total
/// entries. Answers: does the synchronous path (parallel across roots) still win when there are
/// many catalogs (e.g. 100), where the work-stealing async path could parallelize within roots too?
///
///   dotnet run -c Release --filter *MultiCatalog*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class MultiCatalogSearchBenchmarks
{
    private const int TotalEntries = 1_000_000;

    private IList<RootEntry> _roots = null!;

    // 1 = single big catalog; 100 = the user's many-catalogs scenario (10k entries each).
    [Params(1, 10, 100)]
    public int RootCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var perRoot = TotalEntries / RootCount;
        var roots = new List<RootEntry>(RootCount);
        for (var i = 0; i < RootCount; i++)
        {
            // Distinct seed per root so the catalogs differ, like a real multi-drive load.
            roots.Add(SyntheticCatalog.Generate(perRoot, withHashes: false, seed: 1000 + i));
        }
        _roots = roots;
    }

    private FindOptions MakeOptions(StrongBox<int> counter) => new()
    {
        Pattern = "zzzznomatchzzzz", // full scan — measures pure traversal throughput
        RegexMode = false,
        IncludePath = false,
        IncludeFiles = true,
        IncludeFolders = true,
        LimitResultCount = int.MaxValue,
        VisitorFunc = (_, _) => { Interlocked.Increment(ref counter.Value); return true; },
    };

    [Benchmark(Baseline = true, Description = "sync Find (parallel across roots)")]
    public int Sync()
    {
        var c = new StrongBox<int>();
        MakeOptions(c).Find(_roots);
        return c.Value;
    }

    [Benchmark(Description = "legacy async FindAsync (work-stealing)")]
    public int Async()
    {
        var c = new StrongBox<int>();
        MakeOptions(c).FindAsync(_roots).GetAwaiter().GetResult();
        return c.Value;
    }
}
