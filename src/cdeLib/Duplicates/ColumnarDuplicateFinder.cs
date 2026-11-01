using System;
using System.Collections.Generic;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using cdeLib.Output;

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
    private readonly ILogger _logger;
    private readonly int _sizesPerPass;

    public ColumnarDuplicateFinder(ILogger logger, int sizesPerPass = ColumnarSizeGrouping.DefaultSizesPerPass)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizesPerPass);
        _logger = logger;
        _sizesPerPass = sizesPerPass;
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

    private static bool IsCandidate(ReadOnlySpan<byte> bits, ReadOnlySpan<long> size, int i) =>
        ColumnarSizeGrouping.IsCandidate((Flags)bits[i], size[i], SizeCandidates.FullyHashedFiles);

    /// <summary>
    /// Returns the duplicate sets (more than one file with the same size and full hash) sorted by
    /// descending size, logging the same summary lines as the tree-based implementation.
    /// </summary>
    public List<DuplicateSet> GetDuplicateSets(IReadOnlyList<ColumnarCatalogReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);

        // Phase 1: which sizes occur more than once among candidates? (bounded temporary size list)
        var grouping = ColumnarSizeGrouping.FindRepeatedSizes(readers, SizeCandidates.FullyHashedFiles, _sizesPerPass);
        var repeatedSizes = grouping.RepeatedSizes;
        var uniqueSizeKeys = grouping.UniqueSizeCount;

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

    /// <param name="readers">The catalogs to search.</param>
    /// <param name="output">Where the files of each set go as records, numbered from 1 by set (#173); null writes
    /// their paths to the console.</param>
    public void FindDuplicates(IReadOnlyList<ColumnarCatalogReader> readers, IRecordWriter output = null)
    {
        var group = 0L;
        foreach (var dupe in GetDuplicateSets(readers))
        {
            _logger.LogInfo("-------------------------------------- {0}", dupe.Size);
            group++;
            foreach (var e in dupe.Entries)
            {
                var reader = readers[e.Catalog];
                if (output == null) Console.WriteLine("{0}", reader.FullPath(e.Index));
                else EntryRecords.WriteDuplicate(output, group, reader.FilePath, reader, e.Index);
            }
        }

        output?.Flush();
    }
}
