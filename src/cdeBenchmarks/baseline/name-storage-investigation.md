# Investigation: entry-name memory (UTF-8 / name-pool)

## Measured: names are the dominant per-entry cost

1M-entry fixture, no hashes, after Phases 1+3:

| Name storage | Bytes/entry | Note |
|--------------|------------:|------|
| normal (~16-char unique names) | 198.85 | current |
| shared single 1-char name | **129.23** | isolates everything-except-name-strings |
| **name contribution** | **≈ 69.6 B/entry** | **~35% of total footprint** |

Reproduce: `cdeMemProbe --generate 1000000 [--shared-names] --out x.cde` then `cdeMemProbe x.cde`.

Names are stored today as two interned UTF-16 `string` objects per entry (`_path` = name without
extension, `field` = extension). For a 16-char ASCII name the `_path` object is ~54 B (16 B header +
4 B length + 32 B chars + 2) plus the 8 B reference in the entry. The extension is interned and
shared, so its amortised cost is small; almost all of the ~70 B is the per-file `_path` string.

Two structural costs make this large: (a) UTF-16 spends **2 bytes per char** for names that are
overwhelmingly ASCII, and (b) every name is a **separate heap object** carrying a ~22 B header.

## Option A — UTF-8 `byte[]` per name

Store `_path` as a UTF-8 `byte[]` instead of a `string`.
- **Saves** the char halving only: ~16 B for a 16-char ASCII name → **est. ~15–20 B/entry** (~8–10%).
- **Keeps** the per-name object header (a `byte[]` header ≈ a `string` header).
- **Risk / work (high, hot path):** `DirEntry.Path` is read by the *hottest* operations — find
  `Contains`/regex, `PathCompareWithDirTo` sort, path building, GUI display. If the getter
  reconstructs a `string`, every access allocates and **search regresses massively** (we just made
  it 48×/18× faster). Avoiding that means rewriting matching/sorting to work on `byte` spans:
  - substring search → UTF-8 byte `IndexOf` (fine for ASCII; **case-insensitive non-ASCII is hard**).
  - ordinal-ignore-case sort → byte compare (ASCII ok; Unicode ordering differs).
  - regex → needs a `string`; would still allocate (or a UTF-8 regex engine).
  - loses `string.Intern` dedup of repeated filenames (e.g. `index.html`, `__init__.py` recur a lot)
    unless a `byte[]` dedup pool is added.
- **Format:** `Path` (Key 5) would serialize as bytes → `.cde` format bump (also shrinks the file).

## Option B — name pool (offset into a shared UTF-8 buffer)  ← biggest win

Hold all names of a catalog in one big UTF-8 `byte[]` per `RootEntry`; each entry stores an
`int` offset + `short` length instead of string references.
- **Eliminates the per-name object header entirely** *and* halves char bytes.
- Per file: ~6 B (offset+len) in the entry + ~16 B in the pool = **~22 B vs ~62–70 B today →
  est. ~40 B/entry saved (~20% of footprint)**. Largest lever available, by far.
- **Risk / work (very high):** same hot-path byte-matching rewrite as Option A, **plus** a pool
  built at load, pool growth/lifetime management, dedup strategy (replacing interning), and a
  bigger `.cde` format change. Touches lib + GUI + web + dupes.

## Recommendation

Names are the single biggest remaining memory lever (~35%), so the upside is real and larger than
the file/dir split. But both options rewrite the **hottest** code (the same name comparison that the
Phase 1–2 search wins depend on) and change the `.cde` format, with genuine Unicode-correctness
pitfalls in case-insensitive matching. This is a larger, riskier effort than Phases 1–3 combined and
warrants its own plan + prototype.

Suggested path if pursued: prototype **Option A** behind the existing benchmarks first — convert
`_path` to UTF-8 with byte-based substring/sort matching — and measure BOTH the footprint delta and
the search-time delta on the 1M fixture before deciding whether the saving justifies the hot-path
risk. Only escalate to **Option B** (the pool) if the measured Option-A search impact is acceptable
and the extra ~20 B/entry is needed.
