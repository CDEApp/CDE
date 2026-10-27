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
/// Managed heap grows with the number of duplicate candidates (files whose size occurs more than
/// once), not with the total entry count: unique-size files are only counted.
/// </summary>
public sealed class ColumnarDuplicateFinder
{
    // Sizes are collected per pass; keep one pass's size list to roughly 8 MB so heap use during the
    // size-counting phase stays bounded regardless of catalog size.
    private const int DefaultSizesPerPass = 1 << 20;

    private readonly ILogger _logger;
    private readonly int _sizesPerPass;

    public ColumnarDuplicateFinder(ILogger logger, int sizesPerPass = DefaultSizesPerPass)
    {
        _logger = logger;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizesPerPass);
        _sizesPerPass = sizesPerPass;
    }

    // Mix the bits before taking the modulus so block-aligned sizes (multiples of 4096 etc.) spread
    // evenly across passes instead of piling into bucket 0.
    private static int PassOf(long size, int passes)
    {
        var x = (ulong)size * 0x9E3779B97F4A7C15UL;
        return (int)((x >> 32) % (ulong)passes);
    }

    public readonly record struct EntryRef(int Catalog, int Index);

    private readonly record struct DupeKey(long Size, ulong HashA, ulong HashB);

    public sealed class DuplicateSet
    {
        public DuplicateSet(long size, List<EntryRef> entries)
        {
            Size = size;
            Entries = entries;
        }

        public long Size { get; }
        public List<EntryRef> Entries { get; }
    }

    private static bool IsCandidate(ReadOnlySpan<byte> bits, ReadOnlySpan<long> size, int i)
    {
        var f = (Flags)bits[i];
        return (f & Flags.Directory) != Flags.Directory
               && (f & Flags.HashDone) == Flags.HashDone
               && (f & Flags.PartialHash) != Flags.PartialHash
               && size[i] != 0;
    }

    /// <summary>
    /// Returns the duplicate sets (more than one file with the same size and full hash) sorted by
    /// descending size, logging the same summary lines as the tree-based implementation.
    /// </summary>
    public List<DuplicateSet> GetDuplicateSets(IReadOnlyList<ColumnarCatalogReader> readers)
    {
        // Phase 1: which sizes occur more than once among candidates? Bucketed by size so the
        // temporary size list never exceeds ~sizesPerPass entries.
        long candidates = 0;
        foreach (var r in readers)
        {
            var bits = r.BitFields;
            var size = r.Size;
            for (var i = 1; i < r.Count; i++)
                if (IsCandidate(bits, size, i)) candidates++;
        }

        var passes = (int)Math.Max(1, (candidates + _sizesPerPass - 1) / _sizesPerPass);
        long uniqueSizeKeys = 0;
        var repeatedSizes = new HashSet<long>();
        var sizes = new List<long>((int)Math.Min(candidates, _sizesPerPass));
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
        var groups = new Dictionary<DupeKey, List<EntryRef>>();
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
                    if (groups.TryGetValue(key, out var list)) list.Add(new EntryRef(c, child));
                    else groups[key] = new List<EntryRef>(2) { new(c, child) };
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
