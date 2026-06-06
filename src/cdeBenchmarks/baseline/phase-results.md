# Phase Results vs Baseline

Tracks the measured delta of each optimization phase against the committed Phase 0 baseline
(`footprint-baseline.csv`, `search-baseline.md`). Same fixture, same harness, every phase.

---

## Phase 1 — free non-breaking wins (no format change)

**Changes shipped**
- **S3** — `FindService` (the CLI `cde find` path) now runs the synchronous `FindOptions.Find`
  instead of the work-stealing `FindAsync`. The async path ran every entry through an
  `async Task<bool>` state machine plus `Task.Yield()`/`Task.Delay(0)` every 500/2000 entries.
  (`src/cdeLib/FindService.cs`)
- **R1a** — `FileEntryCount` / `DirEntryCount` changed `long → uint` on `DirEntry`, `RootEntry`,
  `ICommonEntry`. Values were already `(uint)`-truncated at assignment, so semantics are
  unchanged; in-memory only, no catalog format impact.

**Re-sequenced (not done in Phase 1)**
- **S1** (path-build allocation, the ~973 MB below) → moved to **Phase 2**, where carrying the
  running path down the traversal stack makes it allocation-free without duplicated work.
- **S2** (`SearchValues` name prefilter) → dropped as low value: name search is already ~35 ms,
  and its remaining 52 MB is the `DirEntry.Path` getter re-joining the split extension, which a
  prefilter would not remove.

### Search (1,000,000-entry fixture)

| Query | Baseline (async) | Phase 1 (sync) | Speedup |
|-------|-----------------:|---------------:|--------:|
| substring, name, no match | 1746 ms | **36.3 ms** | 48× |
| substring, name, ~8% (.txt) | 1722 ms | **35.4 ms** | 49× |
| substring, path, no match | 1755 ms | **235.6 ms** | 7.4× |
| substring, path, ~8% (.txt) | 1774 ms | **243.0 ms** | 7.3× |
| regex, name, no match | 1817 ms | **59.6 ms** | 30× |
| regex, path, ~8% (`\.txt$`) | 1828 ms | **253.1 ms** | 7.2× |

Allocation is unchanged by Phase 1 (name 52.7 MB, path 972.7 MB) — that is S1, targeted in Phase 2.
The retained `LEGACY-async*` benchmarks still measure ~1710–1764 ms, confirming the old path.

### Footprint (retained managed heap)

| Entries | Hashes | Baseline B/entry | Phase 1 B/entry | Saved |
|--------:|:------:|-----------------:|----------------:|------:|
| 1,000,000 | no | 211.94 | **203.94** | 8.00 B/entry |

Exactly the predicted 8 bytes/entry (two `long`→`uint`). At 1M entries: 202.1 MB → 194.5 MB managed.

### Correctness
- `cdeLibTest`: 126 passed, 0 failed (7 skipped).
- Full solution (`cde.slnx`) builds clean.
- Note: `cdeLibSpec` / `cdeLibSpec2` are legacy `net48` projects incompatible with the `net10`
  library and do not restore — pre-existing, unrelated to this work, not in the solution.

---

## Phase 2 — allocation-free path search (S1), no format change

**Changes shipped**
- Reimplemented `EntryHelper.MakeFullPath` as a single pass that walks the `ParentCommonEntry`
  chain into the shared `StringBuilder`, instead of the old recursion that allocated a fresh
  full-path string at every ancestor level. All path-building callers (find, dupes, GUI, dump)
  benefit. (`src/cdeLib/Entities/EntryHelper.cs`)
- Added `EntryHelper.FullPathContains` — builds the path into a pooled `char[]` and matches over a
  `Span<char>` (`MemoryExtensions.Contains`), allocating no result string. Wired into the find
  substring path matcher. (`src/cdeLib/Entities/EntryHelper.cs`, `src/cdeLib/FindOptions.cs`)

**Re-sequencing note:** the original Phase 2 also dropped `ParentCommonEntry` (~8 B/entry). Code
evidence shows `.FullPath` / `GetListFromRoot` are called only on **directories** or via
`PairDirEntry` (which carries an explicit parent) — files never need a standalone parent pointer.
The cdeWin GUI depends heavily on directory parent pointers, so removing the field from *all*
entries is a large, risky GUI refactor for the same 8 B that R1a already delivered safely. The
parent pointer can only be reclaimed from **files**, which requires the file/dir type split — so
`ParentCommonEntry` removal folds into **Phase 3**, validated by the split.

### Search (1,000,000-entry fixture) — cumulative

| Query | Baseline | Phase 1 | Phase 2 | Alloc (base → P2) |
|-------|---------:|--------:|--------:|------------------:|
| substring, path, no match | 1755 ms | 236 ms | **101.6 ms** | 978.7 MB → **52.7 MB** |
| substring, path, ~8% (.txt) | 1774 ms | 243 ms | **96.4 ms** | 978.7 MB → **52.7 MB** |
| regex, path, ~8% (`\.txt$`) | 1828 ms | 253 ms | **136 ms** | 1349 MB → **265 MB** |

Path-search allocation cut ~94.6% (residual 52.7 MB is the per-file `DirEntry.Path` extension
rejoin, same as name search). Name-search numbers are unchanged from Phase 1.

### Correctness
- `cdeLibTest`: 126 passed (incl. all `EntryHelper` / `GetListFromRoot` / `RootEntry` path tests
  that assert exact path strings — confirms the single-pass build is identical).
- Full solution builds clean.
