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
