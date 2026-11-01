# Rescan timing: where the time goes (#172)

Research note. No production code changed. Numbers come from a throwaway console harness (not committed) that
repeats the phases of `CatalogSession.RescanCore` against `cdeLib` directly: `RootEntry.PopulateRoot`,
`ColumnarHashReuse.CopyHashes` (the real carry-forward code), `SortAllChildrenByPath` + `SetSummaryFields`,
and `ColumnarFormat.WriteDirect`. The session swap step (file renames plus an mmap open) is not measured.

## Machine and trees

- AMD Ryzen 9 5950X (16 cores / 32 threads), 64 GB RAM, Windows 11 23H2, .NET 10, Release build, workstation GC.
- Trees are on a local NVMe SSD (C:). Windows Defender real-time scanning was left on, as for a normal user.
- Tree A: `C:\Users\jason\.nuget\packages`, 170,107 entries (127,829 files, 42,278 dirs). Many small dirs.
- Tree B: `C:\Program Files\dotnet`, 199,402 entries (179,198 files, 20,204 dirs). Fewer, fuller dirs.
- The "old" catalog was a `.cdex` of the same tree with every file hashed (worst case for carry-forward work).

## Results (ms)

Warm = mean of runs 1 to 3 in one process. Run 0 = first run in a fresh process.

| Phase | Tree A warm | Tree A run 0 | Tree B warm | Tree B run 0 |
|---|---|---|---|---|
| 1. Walk the file system | 4,413 | 5,838 | 2,233 | 1,926 |
| 2. Carry hashes forward | 31 | 103 | 43 | 85 |
| 3a. Sort + summary fields | 60 | 65 | 62 | 94 |
| 3b. Write `.cdex` (12-15 MB) | 34 | 98 | 48 | 111 |
| Total | 4,538 | 6,104 | 2,387 | 2,216 |
| Walk share of total | 97% | 96% | 94% | 87% |

"Few files changed": 10 files were given a new modified time, so their hashes cannot be reused. Carry-forward
reused 127,819 of 127,829 hashes (Tree A) and 179,188 of 179,198 (Tree B). Totals were within noise of the
unchanged case (Tree A 4.6-4.9 s, Tree B 2.2 s). The pipeline is not incremental, so cost does not depend on how
much changed. Only the number of files the next `cde hash` must re-read changes.

## Noise and limits

- Walk time varies about 10% between runs on the same tree (Defender, indexer, other processes). Treat the sub-100 ms phases as order of magnitude only.
- "Cold" here means a new process (run 0). The OS metadata cache was warm because the trees had just been enumerated. A true cold-cache run needs an admin cache flush or a reboot and was not done. Cold walks, and walks on HDD or network shares, will be slower, so the walk share would be larger, not smaller.
- Only a local SSD and two trees of about 200k entries. Nothing was measured on HDD, network shares or multi-million-entry trees. Scaling is expected to be roughly linear in entry count.
- The directory-stat bound below was measured serially with path-based `Directory.GetLastWriteTimeUtc`; a handle-relative or parallel stat could be cheaper.

## Reading the numbers

Rescan is a walk. Carry-forward, sort and write together are 3-13% of the time (under 300 ms per 200k entries) and
are not worth tuning. Any speed-up has to cut the walk.

## Candidate speed-ups for the walk

**Skip directories whose modified time is unchanged.** Upper-bound test: stat every directory by path, with no
enumeration. That took 2.9-3.4 s for Tree A's 42k dirs and 1.2-1.6 s for Tree B's 20k dirs, which is 65-77% and
55-73% of the full walk. So the best-case saving is roughly 25-40% of the walk, and the real saving is lower
because changed directories still need a full enumeration. The risk is worse than the saving. On NTFS a directory's
modified time changes only when entries are added, removed or renamed in it. Editing a file in place (content, size
or timestamp) does not touch the parent directory, and the catalog stores size and modified time per file, so a skip
scheme would silently keep stale values for every in-place edit. Some network and non-NTFS file systems also do not
maintain directory times reliably. Not recommended as a default.

**NTFS USN change journal.** Turns a rescan from O(entries) into O(changes), potentially 10-100x on large, mostly
static trees, and it sees in-place edits. Costs: NTFS/ReFS on Windows only (no help for Linux, macOS, network
shares or FAT/exFAT media), a volume handle (usually elevation), and a journal that can wrap or be reset, so the
catalog must store the last USN and journal id and fall back to a full walk when they do not match. Large design
and test surface. Not measured here; the 10-100x figure is an estimate, not a result.

**Cheaper options first** (no missed-change risk): find out why the walk manages only 38k-90k entries/s on a
32-thread machine (worker count, per-entry allocations, per-directory handle opens, Defender cost), and check whether
the enumeration can use large-fetch directory reads.

## Recommendation

Leave the carry-forward and write phases alone. Do not ship mtime-based directory skipping: it saves at most about a
third of the walk and silently misses in-place file edits. The walk is the only target, so the next step is a
profiling pass over it (cheap, no correctness risk). Consider a USN-journal fast path, opt-in and Windows-only, only
if profiling shows the walk cannot get much faster and users report slow rescans of large volumes.

## Follow-up ideas (not raised as issues)

- Profile the walk (dotTrace) on this machine and on an HDD; target allocations and syscalls per entry.
- Repeat the measurement on a multi-million-entry tree, a network share and a true cold cache before deciding on the USN journal.
- Prototype a USN-journal reader behind an interface that falls back to the full walk on any doubt.
