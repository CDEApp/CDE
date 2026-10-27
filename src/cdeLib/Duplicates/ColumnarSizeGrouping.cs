using System;
using System.Collections.Generic;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;

namespace cdeLib.Duplicates;

/// <summary>Which files take part in a size grouping.</summary>
internal enum SizeCandidates
{
    /// <summary>Every non-empty file: the files <c>cde hash</c> considers.</summary>
    NonEmptyFiles,

    /// <summary>Non-empty files with a usable full hash: the files <c>cde dupes</c> compares.</summary>
    FullyHashedFiles,
}

/// <summary>
/// Finds the file sizes that occur more than once across memory-mapped catalogs, without a pointer tree.
/// Sizes are bucketed (by a mixed hash of the size) over several passes so the temporary size list holds
/// about sizesPerPass entries at a time. The pass count comes from the readers' header entry counts (an
/// upper bound on the candidate count), so no separate counting scan is needed. The returned set grows with
/// the number of files in a repeated-size bucket, not with the total entry count.
/// </summary>
internal static class ColumnarSizeGrouping
{
    // Sizes are collected per pass; one pass's size list is about 8 MB (1M longs) at this default.
    public const int DefaultSizesPerPass = 1 << 20;

    public sealed class Result
    {
        public HashSet<long> RepeatedSizes { get; } = [];

        /// <summary>Sizes held by exactly one candidate.</summary>
        public long UniqueSizeCount { get; set; }

        /// <summary>Candidates whose size is repeated.</summary>
        public long RepeatedFileCount { get; set; }

        /// <summary>Largest number of candidates sharing one size, or -1 when no size repeats.</summary>
        public int LongestRun { get; set; } = -1;

        public long LongestRunSize { get; set; }
    }

    public static bool IsCandidate(Flags f, long size, SizeCandidates which) =>
        (f & Flags.Directory) != Flags.Directory
        && size != 0
        && (which == SizeCandidates.NonEmptyFiles || (f.IsHashUsable() && (f & Flags.PartialHash) == 0));

    // Mix the bits before taking the modulus so block-aligned sizes (multiples of 4096 etc.) spread
    // evenly across passes instead of piling into bucket 0.
    private static int PassOf(long size, int passes)
    {
        var x = (ulong)size * 0x9E3779B97F4A7C15UL;
        return (int)((x >> 32) % (ulong)passes);
    }

    public static Result FindRepeatedSizes(IReadOnlyList<ColumnarCatalogReader> readers, SizeCandidates which,
        int sizesPerPass = DefaultSizesPerPass)
    {
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizesPerPass);

        long totalEntries = 0;
        foreach (var r in readers) totalEntries += r.Count;

        var passes = (int)Math.Max(1, (totalEntries + sizesPerPass - 1) / sizesPerPass);
        var result = new Result();
        var sizes = new List<long>();
        for (var pass = 0; pass < passes; pass++)
        {
            sizes.Clear();
            foreach (var r in readers)
            {
                var bits = r.BitFields;
                var size = r.Size;
                for (var i = 1; i < r.Count; i++)
                    if (IsCandidate((Flags)bits[i], size[i], which) && (passes == 1 || PassOf(size[i], passes) == pass))
                        sizes.Add(size[i]);
            }

            sizes.Sort();
            for (var s = 0; s < sizes.Count;)
            {
                var e = s + 1;
                while (e < sizes.Count && sizes[e] == sizes[s]) e++;
                var run = e - s;
                if (run == 1)
                {
                    result.UniqueSizeCount++;
                }
                else
                {
                    result.RepeatedSizes.Add(sizes[s]);
                    result.RepeatedFileCount += run;
                    if (run > result.LongestRun)
                    {
                        result.LongestRun = run;
                        result.LongestRunSize = sizes[s];
                    }
                }

                s = e;
            }
        }

        return result;
    }
}
