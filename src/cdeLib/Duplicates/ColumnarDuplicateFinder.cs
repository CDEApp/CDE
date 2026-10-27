using System;
using System.Collections.Generic;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;

namespace cdeLib.Duplicates;

/// <summary>
/// Finds duplicate files directly over memory-mapped <see cref="ColumnarCatalogReader"/>s without
/// materialising a pointer tree. Entries are addressed as (catalog, index) value pairs; only the
/// size, flag and hash columns are read, and full paths are built only for printed duplicates.
///
/// Equality, filtering, traversal order, set order and log lines replicate the tree-based
/// <see cref="Duplication.FindDuplicates(IEnumerable{RootEntry})"/> exactly: a file is a candidate when it
/// is a non-empty, non-directory entry with a completed full hash; two candidates are duplicates when
/// size and 16-byte hash both match.
///
/// Memory: the temporary size list is bounded by roughly sizesPerPass entries (about 8 MB by default)
/// regardless of catalog size. The retained state - the set of repeated sizes and the (size, hash)
/// groups - grows with the number of files that sit in a repeated-size bucket (every file sharing its
/// size with another candidate), not with the total entry count: unique-size files are only counted.
/// This is bounded by the candidate files, but it is not itself bounded by sizesPerPass.
/// </summary>
public sealed class ColumnarDuplicateFinder
{
    // Sizes are collected per pass; one pass's size list is about 8 MB (1M longs) at this default.
    private const int DefaultSizesPerPass = 1 << 20;

    private readonly ILogger _logger;
    private readonly int _sizesPerPass;

    public ColumnarDuplicateFinder(ILogger logger, int sizesPerPass = DefaultSizesPerPass)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizesPerPass);
        _logger = logger;
        _sizesPerPass = sizesPerPass;
    }

    // Mix the bits before taking the modulus so block-aligned sizes (multiples of 4096 etc.) spread
    // evenly across passes instead of piling into bucket 0.
    private static int PassOf(long size, int passes)
    {
        var x = (ulong)size * 0x9E3779B97F4A7C15UL;
        return (int)((x >> 32) % (ulong)passes);
    }

    public readonly record struct CatalogEntry(int Catalog, int Index);

    private readonly record struct DupeKey(long Size, ulong HashA, ulong HashB);

    public sealed class DuplicateSet
    {
        public DuplicateSet(long size, List<CatalogEntry> entries)
        {
            Size = size;
            Entries = entries;
        }

        public long Size { get; }
        public List<CatalogEntry> Entries { get; }
    }

    private static bool IsCandidate(ReadOnlySpan<byte> bits, ReadOnlySpan<long> size, int i)
    {
        var f = (Flags)bits[i];
        return (f & Flags.Directory) != Flags.Directory
               && f.IsHashUsable()
               && (f & Flags.PartialHash) == 0
               && size[i] != 0;
    }

    /// <summary>
    /// Returns the duplicate sets (more than one file with the same size and full hash) sorted by
    /// descending size, logging the same summary lines as the tree-based implementation.
    /// </summary>
    public List<DuplicateSet> GetDuplicateSets(IReadOnlyList<ColumnarCatalogReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);

        // Phase 1: which sizes occur more than once among candidates? Sizes are bucketed (by a mixed
        // hash of the size) over several passes so the temporary size list holds about
        // sizesPerPass entries at a time. The pass count comes from the readers' header entry counts
        // (an upper bound on the candidate count), so no separate counting scan is needed.
        long totalEntries = 0;
        foreach (var r in readers) totalEntries += r.Count;

        var passes = (int)Math.Max(1, (totalEntries + _sizesPerPass - 1) / _sizesPerPass);
        long uniqueSizeKeys = 0;
        var repeatedSizes = new HashSet<long>();
        var sizes = new List<long>();
        for (var pass = 0; pass < passes; pass++)
        {
            sizes.Clear();
            foreach (var r in readers)
            {
                var bits = r.BitFields;
                var size = r.Size;
                for (var i = 1; i < r.Count; i++)
                    if (IsCandidate(bits, size, i) && (passes == 1 || PassOf(size[i], passes) == pass))
                        sizes.Add(size[i]);
            }

            sizes.Sort();
            for (var s = 0; s < sizes.Count;)
            {
                var e = s + 1;
                while (e < sizes.Count && sizes[e] == sizes[s]) e++;
                if (e - s == 1) uniqueSizeKeys++;
                else repeatedSizes.Add(sizes[s]);
                s = e;
            }
        }

        sizes = null;

        // Phase 2: traverse in the same order as the tree walk and group only files with a repeated
        // size by (size, hash). Dictionary enumeration is insertion order (no removals), matching the
        // original first-seen set order.
        var groups = new Dictionary<DupeKey, List<CatalogEntry>>();
        var stack = new Stack<int>();
        for (var c = 0; c < readers.Count; c++)
        {
            var r = readers[c];
            if (r.FirstChildOf(0) == EntryStore.None) continue;
            var bits = r.BitFields;
            var size = r.Size;
            stack.Push(0);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                for (var child = r.FirstChildOf(current); child != EntryStore.None; child = r.NextSiblingOf(child))
                {
                    var f = (Flags)bits[child];
                    if ((f & Flags.Directory) == Flags.Directory)
                    {
                        if (r.FirstChildOf(child) != EntryStore.None) stack.Push(child);
                        continue;
                    }

                    if (!IsCandidate(bits, size, child) || !repeatedSizes.Contains(size[child])) continue;

                    var hash = r.HashOf(child);
                    var key = new DupeKey(size[child], hash.HashA, hash.HashB);
                    if (groups.TryGetValue(key, out var list)) list.Add(new CatalogEntry(c, child));
                    else groups[key] = new List<CatalogEntry>(2) { new(c, child) };
                }
            }
        }

        var moreThanOne = new List<DuplicateSet>();
        foreach (var kvp in groups)
            if (kvp.Value.Count > 1)
                moreThanOne.Add(new DuplicateSet(kvp.Key.Size, kvp.Value));

        _logger.LogInfo("Count of list of all hashes of files with same sizes {0}", uniqueSizeKeys + groups.Count);
        _logger.LogInfo("Count of list of all hashes of files with same sizes where more than 1 of that hash {0}",
            moreThanOne.Count);

        moreThanOne.Sort((a, b) => b.Size.CompareTo(a.Size));
        return moreThanOne;
    }

    public void FindDuplicates(IReadOnlyList<ColumnarCatalogReader> readers)
    {
        foreach (var dupe in GetDuplicateSets(readers))
        {
            _logger.LogInfo("-------------------------------------- {0}", dupe.Size);
            foreach (var e in dupe.Entries)
                Console.WriteLine("{0}", readers[e.Catalog].FullPath(e.Index));
        }
    }
}
