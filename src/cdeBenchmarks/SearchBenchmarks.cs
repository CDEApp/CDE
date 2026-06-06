using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using cdeLib;
using cdeLib.Entities;

namespace cdeBenchmarks;

/// <summary>
/// Search (find) throughput across the matrix {substring, regex} × {name, path}, against a fixed
/// in-memory synthetic catalog loaded once. The visitor only counts matches (no console I/O) so the
/// measurement reflects traversal + matching cost, not output.
///
/// Patterns are chosen with known hit-rates:
///   *NoMatch  – 0 hits  → pure full-scan + match throughput (worst case, most representative of cost)
///   *Common   – ~8% hits (".txt") → includes per-match visitor + full-path-build cost
///
///   dotnet run -c Release --filter *Search*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class SearchBenchmarks
{
    private IList<RootEntry> _roots = null!;

    [Params(1_000_000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Hashes are irrelevant to search cost; use the no-hash fixture.
        _roots = new List<RootEntry> { CatalogFixture.BuildInMemory(EntryCount, withHashes: false) };
    }

    private FindOptions MakeOptions(string pattern, bool regexMode, bool includePath, StrongBox<int> counter)
        => new()
        {
            Pattern = pattern,
            RegexMode = regexMode,
            IncludePath = includePath,
            IncludeFiles = true,
            IncludeFolders = true,
            LimitResultCount = int.MaxValue,
            VisitorFunc = (_, _) =>
            {
                Interlocked.Increment(ref counter.Value);
                return true;
            },
        };

    // Production path: synchronous Find (plain bool delegate, no per-entry async state machine).
    // FindService routes the CLI through this as of Phase 1.
    private int RunFind(string pattern, bool regexMode, bool includePath)
    {
        var counter = new StrongBox<int>();
        MakeOptions(pattern, regexMode, includePath, counter).Find(_roots);
        return counter.Value;
    }

    // Legacy work-stealing async path, kept only to track the gap that motivated the Phase 1 switch.
    private int RunFindAsyncLegacy(string pattern, bool regexMode, bool includePath)
    {
        var counter = new StrongBox<int>();
        MakeOptions(pattern, regexMode, includePath, counter).FindAsync(_roots).GetAwaiter().GetResult();
        return counter.Value;
    }

    [Benchmark(Description = "substring, name, no match (full scan)")]
    public int SubstringNameNoMatch() => RunFind("zzzznomatchzzzz", regexMode: false, includePath: false);

    [Benchmark(Description = "substring, name, ~8% hits (.txt)")]
    public int SubstringNameCommon() => RunFind(".txt", regexMode: false, includePath: false);

    [Benchmark(Description = "substring, path, no match (full scan + path build)")]
    public int SubstringPathNoMatch() => RunFind("zzzznomatchzzzz", regexMode: false, includePath: true);

    [Benchmark(Description = "substring, path, ~8% hits (.txt)")]
    public int SubstringPathCommon() => RunFind(".txt", regexMode: false, includePath: true);

    [Benchmark(Description = "regex, name, no match (full scan)")]
    public int RegexNameNoMatch() => RunFind("zzzz[0-9]nomatch", regexMode: true, includePath: false);

    [Benchmark(Description = "regex, path, ~8% hits (\\.txt$)")]
    public int RegexPathCommon() => RunFind(@"\.txt$", regexMode: true, includePath: true);

    // --- Legacy async path (deprecated for the CLI in Phase 1; kept to track the gap) ---

    [Benchmark(Description = "LEGACY-async substring, name, no match")]
    public int LegacySubstringNameNoMatch() => RunFindAsyncLegacy("zzzznomatchzzzz", regexMode: false, includePath: false);

    [Benchmark(Description = "LEGACY-async substring, path, no match")]
    public int LegacySubstringPathNoMatch() => RunFindAsyncLegacy("zzzznomatchzzzz", regexMode: false, includePath: true);
}
