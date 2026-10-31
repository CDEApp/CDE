using BenchmarkDotNet.Attributes;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;

namespace cdeBenchmarks;

/// <summary>
/// The Everything-style query mode against the plain substring mode, over a memory-mapped columnar catalog (the
/// production scan, <see cref="ColumnarCatalogReader.Find"/>). A single Everything term should cost about the same as
/// the plain substring search; the multi-term cases show the price of the expression on top of that. Patterns are
/// full scans with 0 hits (worst case) unless noted.
///
///   dotnet run -c Release --filter *EverythingSearch*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class EverythingSearchBenchmarks
{
    private const int EntryCount = 1_000_000;

    private string _file = null!;
    private ColumnarCatalogReader _reader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _file = Path.Combine(Path.GetTempPath(), $"cdebench-everything-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(CatalogFixture.BuildInMemory(EntryCount, withHashes: false), _file);
        _reader = new ColumnarCatalogReader(_file);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reader.Dispose();
        File.Delete(_file);
    }

    private int Run(string pattern, bool everything, bool includePath)
    {
        var count = 0;
        _reader.Find(
            new EntryStoreFindOptions { Pattern = pattern, EverythingMode = everything, IncludePath = includePath },
            _ => count++);
        return count;
    }

    [Benchmark(Baseline = true, Description = "plain substring, name, no match")]
    public int PlainNameNoMatch() => Run("zzzznomatchzzzz", everything: false, includePath: false);

    [Benchmark(Description = "everything, one term, name, no match")]
    public int EverythingOneTermNoMatch() => Run("zzzznomatchzzzz", everything: true, includePath: false);

    [Benchmark(Description = "everything, 'a b' AND, name, no match")]
    public int EverythingAndNoMatch() => Run("zzzznomatchzzzz yyyynomatchyyyy", everything: true, includePath: false);

    [Benchmark(Description = "everything, '<.txt | .log> !tmp', name, common hits")]
    public int EverythingGroupCommon() => Run("<.txt | .log> !tmp", everything: true, includePath: false);

    [Benchmark(Description = "everything, 'file: term', name, no match (kind test first)")]
    public int EverythingFileFilterNoMatch() => Run("file: zzzznomatchzzzz", everything: true, includePath: false);

    [Benchmark(Description = "everything, 'folder:' alone, name, folders are hits")]
    public int EverythingFolderFilterOnly() => Run("folder:", everything: true, includePath: false);

    [Benchmark(Description = "plain substring, path, no match")]
    public int PlainPathNoMatch() => Run("zzzznomatchzzzz", everything: false, includePath: true);

    [Benchmark(Description = "everything, one term, path, no match")]
    public int EverythingOneTermPathNoMatch() => Run("zzzznomatchzzzz", everything: true, includePath: true);

    [Benchmark(Description = "everything, path: term, no match (path built per entry)")]
    public int EverythingPathModifierNoMatch() => Run("path:zzzznomatchzzzz", everything: true, includePath: false);

    [Benchmark(Description = "everything, name term misses first, then path: term never reached")]
    public int EverythingNameFirstShortCircuit() => Run("zzzznomatchzzzz path:zzzznomatchzzzz", everything: true, includePath: false);
}
