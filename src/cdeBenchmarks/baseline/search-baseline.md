# Search Baseline — 2026-06-06

BenchmarkDotNet, .NET 10, Server GC. Fixture: 1,000,000-entry synthetic catalog (seed 42),
in memory. Driven through the production path `FindOptions.FindAsync` (what `FindService` /
`cde find` uses). Visitor counts matches only — no console I/O.

| Method | Mean | StdDev | Allocated |
|--------|-----:|-------:|----------:|
| substring, name, no match (full scan)              | 1.746 s | 0.043 s |    58.7 MB |
| substring, name, ~8% hits (.txt)                   | 1.722 s | 0.007 s |    58.7 MB |
| substring, path, no match (full scan + path build) | 1.755 s | 0.026 s |   978.7 MB |
| substring, path, ~8% hits (.txt)                   | 1.774 s | 0.012 s |   978.7 MB |
| regex, name, no match (full scan)                  | 1.817 s | 0.079 s |   460.9 MB |
| regex, path, ~8% hits (`\.txt$`)                   | 1.828 s | 0.006 s |  1349.0 MB |

## What this reveals (drives Phase 1)

- **Path search allocates ~0.98–1.35 GB per single 1M-entry query.** `GetPatternMatcher` builds a
  full-path string (`EntryHelper.MakeFullPathPooled`) for *every candidate* — and for a no-match
  query that's every entry. **S1**: match over pooled `Span<char>` (`MemoryExtensions.Contains`)
  instead of allocating a string per entry; pass the already-built path to the visitor on a match
  rather than rebuilding it in `FindService.FindAsync`. Target: path allocation → near-zero.

- **Even name-only, no-match scan is ~1.7 s and allocates 58.7 MB** for what should be an
  allocation-free linear scan. The cost is the per-entry `async Task<bool>` processor
  (`CreateAsyncProcessor`) plus `Task.Yield()` / `Task.Delay(0)` every 500 / 2000 entries. The
  synchronous `Find` path (`GetFindFunc`, a plain `bool` delegate) has none of this. **S3**: route
  the CLI through the synchronous path (or make parallel/serial adaptive) and drop per-entry yields.

- Mean time is dominated by async overhead, not matching: name-no-match (58 MB alloc) and
  path-no-match (978 MB alloc) have nearly identical ~1.75 s means. Cutting the async overhead
  should move the needle more than matching micro-opts.

These numbers are the bar Phase 1 must beat. Re-run with
`dotnet run --project src/cdeBenchmarks -c Release -- --filter *Search*`.
