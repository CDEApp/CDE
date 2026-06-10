# Memory & Search Baseline (Phase 0)

This directory holds the committed baseline that every memory/search optimization phase is
measured against. The goal of the work: **reduce the in-memory footprint of a loaded catalog
(primary)** and **improve search performance (secondary)**. A catalog format-version bump is
permitted in later phases.

All phases must re-run the *same* harness against the *same* synthetic fixture (same seed) so
deltas are attributable to the change under test, not to fixture drift.

## Harness

| Tool | Project | Measures |
|------|---------|----------|
| `cdeMemProbe` | `src/cdeMemProbe` | **Retained** managed heap + working set after load, bytes/entry, load wall-clock |
| `CatalogLoadBenchmarks` | `src/cdeBenchmarks` | Load wall-clock + allocations (BenchmarkDotNet) |
| `SearchBenchmarks` | `src/cdeBenchmarks` | Find throughput across {substring,regex} × {name,path} |
| `SyntheticCatalog` | `src/cdeMemProbe` | Shared deterministic fixture generator (seed 42, ~50:1 file:dir) |

`cdeMemProbe` is a separate minimal process on purpose: BenchmarkDotNet's `MemoryDiagnoser`
reports *allocations during a run*, not the *steady-state retained heap* — which is the headline
metric here. The probe loads one catalog, settles the GC (`Collect` ×2 + `WaitForPendingFinalizers`),
then reports `GC.GetTotalMemory(true)` and process working/private set.

## Reproduce

```powershell
# Build
dotnet build src/cdeMemProbe/cdeMemProbe.csproj -c Release
dotnet build src/cdeBenchmarks/cdeBenchmarks.csproj -c Release

# Footprint: generate a fixture, then measure it in a clean process
$probe = "src/cdeMemProbe/bin/Release/net10.0/cdeMemProbe.dll"
dotnet $probe --generate 1000000 --out $env:TEMP\fix-1m.cde
dotnet $probe $env:TEMP\fix-1m.cde            # add --hashes to --generate for the hashed variant

# Load timing + allocations
dotnet run --project src/cdeBenchmarks -c Release -- --filter *CatalogLoad*

# Search timings
dotnet run --project src/cdeBenchmarks -c Release -- --filter *Search*
```

## Baseline results — 2026-06-06

Machine: Windows 11, .NET 10, Server GC. Synthetic fixture, seed 42, ~50 files per directory.
Raw footprint rows in [`footprint-baseline.csv`](./footprint-baseline.csv).

### Footprint (retained managed heap after load)

| Entries | Hashes | Managed heap | Working set | **Bytes / entry** | Load ms |
|--------:|:------:|-------------:|------------:|------------------:|--------:|
| 1,000,000 | no  | 202.1 MB | 356.5 MB | **211.94** | 1385 |
| 1,000,000 | yes | 207.0 MB | 373.8 MB | **217.07** | 1290 |
| 10,000,000 | no | 2018.7 MB | 2887.0 MB | **211.67** | 8447 |

Key observation: hashed vs no-hash footprint is nearly identical (+~5 B/entry), because the
16-byte `Hash16` struct is stored **inline on every entry whether or not a hash is set**. This is
the direct evidence behind the Phase 3 plan to move hashes off-entry into a side table
(~16 B/entry reclaimable in the common no-hash case).

### Search timings (1,000,000-entry fixture)

See [`search-baseline.md`](./search-baseline.md) for the full BenchmarkDotNet table.
