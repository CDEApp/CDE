# Spike: zero-copy columnar mmap catalog format

Branch `spike/flatbuffer-mmap-search`. Goal: prove (or kill) the idea of replacing in-memory
catalog load with a **columnar struct-of-arrays file read zero-copy over a memory map**, so that
"load" becomes `mmap` and search streams over the mapping — managed heap ≈ 0, working set ≈ pages
actually touched (reclaimable OS page cache).

Spike code lives in `src/cdeMemProbe/Columnar/` (`ColumnarFormat` writer, `ColumnarReader` mmap
reader) plus `--migrate` / `--flat` modes in `Program.cs`. It is intentionally **not** in
production `cdeLib` until the thesis was confirmed. Format magic `CDEX`, version 1.

## Result: thesis confirmed, decisively

Real catalog `C-System-C__program files.cde`, **649,377 entries**:

| Model | Managed heap | B/entry (heap) | "Load"/open | Notes |
|---|---:|---:|---:|---|
| Tree (MessagePack, current) | 82.3 MB | 126.7 | 1123 ms | full pointer graph |
| In-memory SoA store | 43.8 MB | 67.4 | 1050 ms | parallel arrays on heap |
| **Columnar mmap** | **0.07 MB** | **0.11** | **3 ms** | catalog stays in the mapping |

- **Managed heap ~0.** 72 KB total — just the reader object + tiny scan buffers. ~600× below the
  store, ~1100× below the tree. The catalog never enters the GC heap. This is the primary goal
  (footprint) hit as hard as it can be hit.
- **"Load" is ~free:** 3 ms to mmap vs ~1100 ms to deserialize. ~370× faster, and independent of
  catalog size (no parse).
- **Search is zero-alloc:** a full name byte-scan of 649 K entries allocated **40 bytes** total
  (path-walk variant 1088 B, from one growable buffer). Confirms UTF-8 byte-matching avoids the
  FlatSharp lazy-string trap.
- **Search speed (single-threaded):** name `.dll` → 131 958 hits in **26.9 ms** (~24 M entries/s);
  path `common` → 59 011 hits in **287 ms** (slower: rebuilds each full path). Both parallelize by
  index range; path mode has obvious caching wins left on the table.

Hashed 100 K synthetic round-trips correctly (Hash16 column is blittable); allocations still 40 B.

## Honest costs / caveats

1. **Disk +36 %** (35.7 MB vs 26.3 MB MessagePack). Causes: full names stored with repeated
   extensions (no dedup), fixed-width 8-aligned columns, no varint packing. Mitigations: dedup
   `Ext` via a shared-string column, optional per-column compression. Zero-copy wants fixed-width
   numeric columns, so the numeric side stays uncompressed; the win is on the name blob.
2. **Working set ≈ touched pages.** A *full* scan touches the whole NameBlob, so working set rises
   toward file size — but these are clean, file-backed, **reclaimable** pages (evictable, shared),
   not committed GC heap. Selective queries and column-skipping (a name query never pages in
   Size/Modified/Hash) keep it well below file size. The number that matters for GC pressure /
   OOM — managed heap — is ~0 regardless.
3. **ASCII case-folding only** in the spike matcher. Production needs proper ordinal-ignore-case
   over UTF-8 (or a stored case-folded name column).
4. **`int` entry count + `int` name offsets** cap at ~2.1 B entries / 2 GB name blob. The README
   claims "billions"; production must widen offsets to `long` (and the count) to keep headroom.
5. **mmap lifetime / immutability.** The mapping must stay open while a catalog is "loaded";
   the buffer is read-only, so `hash`/`scan`/`dupes` write a *fresh* file (they already re-save).
6. **Hand-rolled, not FlatBuffers.** For dense homogeneous columns a hand-rolled layout gives a
   genuinely alloc-free `MemoryMarshal.Cast` view with no vtable/string-materialization overhead;
   FlatBuffers earns its keep on sparse/optional schemas, which a catalog is not. Same zero-copy
   goal the user asked for, better fit for the data shape.

## Reproduce

```
cdeMemProbe "<cat>.cde"                 # tree heap
cdeMemProbe "<cat>.cde" --store         # in-memory SoA heap
cdeMemProbe --migrate "<cat>.cde" --out cat.cdex
cdeMemProbe --flat cat.cdex --pattern .dll          # name search, zero-alloc
cdeMemProbe --flat cat.cdex --pattern common --path # path search
```

## Recommendation

Promote to production behind a format-version gate: add the columnar writer/reader to `cdeLib`,
make `EntryRef : ICommonEntry` a view over `(buffer, offset)` (the seam already exists from the
SoA work), load `find`/GUI/`dump` straight off the mapping, and ship a one-way `cde migrate`
verb (the `--migrate` mode here is the prototype). `scan`/`hash`/`dupes` keep building a tree and
write the columnar file at save. Address caveats 3 & 4 before shipping.
