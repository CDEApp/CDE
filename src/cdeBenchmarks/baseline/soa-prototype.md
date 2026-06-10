# Prototype: Struct-of-Arrays (SoA) EntryStore

Feasibility + footprint measurement for replacing the pointer-based `DirEntry`/`RootEntry` tree
with parallel value-type arrays (one slot per entry; tree shape via `int` firstChild/nextSibling/
parent indices). Prototype: `src/cdeMemProbe/EntryStore.cs`. Measure:
`cdeMemProbe --soa --generate N [--shared-names] [--hashes]`.

## Measured footprint (1M & 10M fixtures)

| Model | Structural (shared names) | Full (normal names) |
|-------|--------------------------:|--------------------:|
| Pointer tree (current) | 129.23 B/entry | 198.85 B/entry |
| **SoA prototype** | **42.23 B/entry** | 151.39 B/entry |
| **Saving** | **−87 B/entry (−67%)** | −47 B/entry (−24%) |

- Scales linearly: SoA at 10M = **42.11 B/entry** (421 MB) vs the tree's ~1.29 GB of structural
  memory — i.e. SoA holds the same 10M-entry catalog structure in **~1/3 the memory**.
- The "full / normal names" SoA number is inflated only because the prototype stores **un-interned,
  un-split** full names. A production SoA would intern + split names like the tree does, landing the
  total near **~112 B/entry (≈ −44%)**. The clean, name-independent result is the structural row.
- **Hashed catalogs:** the prototype's `--hashes` run did *not* allocate the `Hash16[]` side array
  (the `root.IsHashDone` heuristic is wrong — only files are hashed), so it reads 42 B. A real hashed
  SoA adds 16 B/entry → **~58 B/entry**, still far below the tree's ~145 B/entry hashed structural.

## Why it wins

Per entry, the tree pays a 16 B object header + 8 B-each reference fields + allocation rounding +
per-directory `List<DirEntry>` overhead. SoA pays only packed array slots:
`long modified (8) + long size (8) + string name-ref (8) + byte flags (1) + int firstChild/nextSibling/parent (4+4+4)`
≈ **37 B + negligible per-array overhead** — no per-entry header, references shrink 8 B → 4 B.

Validated working in the prototype: linear name-substring search over `Name[]` and full-path
reconstruction by walking `Parent[]` both produce correct results.

## What a production migration would require (large, multi-day, high risk)

The prototype proves the memory win; shipping it is a core-model rewrite:
- **Serialization** — read/write the arrays (or convert tree↔SoA at load/save). FlatSharp (already
  wired) suits SoA well; format bump.
- **Search / sort / dupes / hashing** — operate on indices instead of objects. Likely *faster*
  (cache locality), but every algorithm is re-pointed; hashing writes back into `Hash[]`.
- **GUI (cdeWin) + web** — navigate via `ICommonEntry`/`FullPath`/`GetListFromRoot`. Bridge with a
  thin `readonly struct EntryRef(store, index) : ICommonEntry` adapter to minimize churn, or rewrite.
- **Construction sites + tests** — the ~40 `new DirEntry(...)` sites and the entity tests.

Risk is highest of all options (it is *the* data model, on the billions-of-entries hot paths) and
the WinForms GUI can't be runtime-validated here. But the payoff is the largest by far: ~−67%
structural memory, the only option that removes the per-entry object header.

## Recommendation

SoA is decisively the highest-impact memory lever — proven, not estimated. Worth doing **if** the
team is prepared for a core-model migration. Suggested phasing: (1) land the SoA store + tree↔SoA
converter + a `EntryRef` adapter behind the existing `ICommonEntry` API so the GUI/dupes keep
working; (2) move search/serialization onto the store; (3) drop the tree. Each phase measured against
this prototype.
