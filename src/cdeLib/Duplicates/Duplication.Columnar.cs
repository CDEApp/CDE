using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Infrastructure;

namespace cdeLib.Duplicates;

public partial class Duplication
{
    /// <summary>
    /// A file whose size occurs more than once across all catalogs, addressed by (catalog, entry index),
    /// with the flags and hash it will be saved with.
    /// </summary>
    private struct HashCandidate
    {
        public int Catalog;
        public int Index;
        public long Size;
        public Flags Flags;
        public Hash16 Hash;
    }

    /// <summary>
    /// Index-based equivalent of <see cref="ApplyHash(IList{RootEntry})"/> that hashes straight from
    /// memory-mapped catalogs without rebuilding pointer trees (issue #40). The same files are hashed with the
    /// same two phases (first-pass hash of every file sharing its size with another, then a full hash of every
    /// set of equal size and first-pass hash), so the catalogs end with the same hashes and flags.
    /// The readers are not changed: the result holds each catalog's new flags and hashes for
    /// <see cref="ColumnarFormat.WriteWithHashes"/>. Hashes computed before a break are kept in the result.
    /// Managed memory grows with the number of files sharing a size, not with the total entry count.
    /// </summary>
    public async Task<ColumnarHashUpdates> ApplyHash(IReadOnlyList<ColumnarCatalogReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);
        _logger.LogDebug("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var grouping = ColumnarSizeGrouping.FindRepeatedSizes(readers, SizeCandidates.NonEmptyFiles);
        var candidates = CollectCandidates(readers, grouping);
        _logger.LogDebug("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        long totalFiles = 0;
        foreach (var r in readers) totalFiles += r.RootFileEntryCount;
        ReportSizeMatches(grouping.RepeatedSizes.Count, totalFiles, candidates.Length, grouping.LongestRun,
            grouping.LongestRunSize);

        // Issue #37: partial hashes from older versions hashed the wrong block and read as "not hashed".
        var legacyPartials = 0;
        foreach (var c in candidates)
        {
            if (!c.Flags.IsHashUsable() && (c.Flags & (Flags.HashDone | Flags.PartialHash)) == (Flags.HashDone | Flags.PartialHash))
            {
                legacyPartials++;
            }
        }

        ReportLegacyPartials(legacyPartials);

        // Largest files first, so a break part way still leaves the large files hashed; one group per
        // drive/share hashes in parallel with the others.
        var groupedByDriveRoot = GroupByDriveRoot(readers, candidates);

        var timer = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var outerOptions = new ParallelOptions { CancellationToken = token };
        _duplicationStatistics.FilesToCheckForDuplicatesCount = candidates.Length;
        var breakReported = 0;

        try
        {
            await Parallel.ForEachAsync(groupedByDriveRoot, outerOptions, async (grp, _) =>
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = 2
                };

                await Parallel.ForEachAsync(grp, parallelOptions, async (c, _) =>
                {
                    _duplicationStatistics.SeenFileSize(candidates[c].Size);
                    await CalculateFirstPassHashAsync(readers, candidates, c);
                    if (_cancellation.IsCancellationRequested)
                    {
                        ReportBreakOnce(ref breakReported, PartialPhaseBreakMessage);
                        await cts.CancelAsync();
                    }
                });
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordUnexpectedFailure(ex, nameof(ApplyHash));
            ReportFileErrors();
            return Updates(readers, candidates);
        }

        ReportPartialPhaseDone(timer);

        // Ctrl-C pressed again while the first-pass phase was stopping means stop the full phase too (Reset would
        // otherwise lose it); otherwise require a fresh break to stop the full hash phase.
        var skipFullHashing = _cancellation.BreakCount > 1;
        _cancellation.Reset();
        if (skipFullHashing) ReportStatus(FullPhaseSkippedMessage);
        else await CompleteFullHashesAsync(readers, candidates);

        timer.Stop();
        ReportHashingDone(timer);
        ReportFileErrors();
        return Updates(readers, candidates);
    }

    /// <summary>The candidates whose flags or usable hash changed, per catalog, by ascending entry index.</summary>
    private static ColumnarHashUpdates Updates(IReadOnlyList<ColumnarCatalogReader> readers, HashCandidate[] candidates)
    {
        var lists = new List<EntryHashUpdate>[readers.Count];
        foreach (var c in candidates)
        {
            var r = readers[c.Catalog];
            if (c.Flags == r.Flags(c.Index) && (!c.Flags.IsHashUsable() || c.Hash == r.HashOf(c.Index)))
            {
                continue; // unchanged
            }

            (lists[c.Catalog] ??= []).Add(new EntryHashUpdate(c.Index, c.Flags, c.Hash));
        }

        var byCatalog = new EntryHashUpdate[readers.Count][];
        for (var k = 0; k < readers.Count; k++)
        {
            var updates = lists[k]?.ToArray() ?? [];
            Array.Sort(updates, static (a, b) => a.Index.CompareTo(b.Index));
            byCatalog[k] = updates;
        }

        return new ColumnarHashUpdates(byCatalog);
    }

    /// <summary>Every non-empty file whose size is repeated, in descending size order.</summary>
    private static HashCandidate[] CollectCandidates(IReadOnlyList<ColumnarCatalogReader> readers,
        ColumnarSizeGrouping.Result grouping)
    {
        var candidates = new HashCandidate[grouping.RepeatedFileCount];
        var n = 0;
        for (var c = 0; c < readers.Count; c++)
        {
            var r = readers[c];
            var bits = r.BitFields;
            var size = r.Size;
            for (var i = 1; i < r.Count; i++)
            {
                var flags = (Flags)bits[i];
                if (!ColumnarSizeGrouping.IsCandidate(flags, size[i], SizeCandidates.NonEmptyFiles)
                    || !grouping.RepeatedSizes.Contains(size[i]))
                {
                    continue;
                }

                if (n == candidates.Length)
                {
                    throw new InvalidOperationException("A catalog changed while it was being hashed.");
                }

                candidates[n++] = new HashCandidate
                {
                    Catalog = c,
                    Index = i,
                    Size = size[i],
                    Flags = flags,
                    Hash = flags.IsHashUsable() ? r.HashOf(i) : default,
                };
            }
        }

        if (n != candidates.Length)
        {
            throw new InvalidOperationException("A catalog changed while it was being hashed.");
        }

        Array.Sort(candidates, static (a, b) =>
        {
            var bySize = b.Size.CompareTo(a.Size);
            if (bySize != 0) return bySize;
            var byCatalog = a.Catalog.CompareTo(b.Catalog);
            return byCatalog != 0 ? byCatalog : a.Index.CompareTo(b.Index);
        });
        return candidates;
    }

    /// <summary>
    /// Candidate positions grouped by the drive/share of their catalog's root, in first-seen order, keeping
    /// the input (descending size) order within each group. Catalogs of the same drive share a group so they
    /// hash serially. Like the tree version, a followed junction onto another volume is grouped with its root.
    /// </summary>
    private static List<List<int>> GroupByDriveRoot(IReadOnlyList<ColumnarCatalogReader> readers,
        HashCandidate[] candidates)
    {
        var groupOfCatalog = new List<int>[readers.Count];
        var byRootString = new Dictionary<string, List<int>>();
        var groups = new List<List<int>>();
        for (var k = 0; k < candidates.Length; k++)
        {
            var catalog = candidates[k].Catalog;
            var group = groupOfCatalog[catalog];
            if (group == null)
            {
                // As the tree version: the root of the catalog's first file, so an empty or relative root
                // path resolves the same way it does when the file is hashed.
                var root = System.IO.Directory.GetDirectoryRoot(readers[catalog].FullPath(candidates[k].Index));
                if (!byRootString.TryGetValue(root, out group))
                {
                    group = [];
                    groups.Add(group);
                    byRootString[root] = group;
                }

                groupOfCatalog[catalog] = group;
            }

            group.Add(k);
        }

        return groups;
    }

    private async Task CalculateFirstPassHashAsync(IReadOnlyList<ColumnarCatalogReader> readers,
        HashCandidate[] candidates, int c)
    {
        if (candidates[c].Flags.IsHashUsable())
        {
            _duplicationStatistics.AddAllreadyDonePartial();
            return;
        }

        // The full path is built only for files that are actually read.
        var path = readers[candidates[c].Catalog].FullPath(candidates[c].Index);
        var hashResponse = await _hashHelper.GetHashResponseFromFile(path, _configuration.HashFirstPassSize);
        if (hashResponse.IsSuccess)
        {
            SetHash(ref candidates[c], hashResponse.Hash, hashResponse.IsPartialHash);
            RecordFirstPassHash(hashResponse, candidates[c].Size);
        }
        else
        {
            _duplicationStatistics.AddFailedToHash();
        }
    }

    // As DirEntry.SetHash(ulong) followed by IsPartialHash = partial.
    private static void SetHash(ref HashCandidate candidate, ulong hash, bool partial)
    {
        candidate.Hash = new Hash16(hash, 0);
        var flags = candidate.Flags | Flags.HashDone;
        candidate.Flags = partial
            ? flags | Flags.PartialHash | Flags.PartialHashV2
            : flags & ~(Flags.PartialHash | Flags.PartialHashV2);
    }

    /// <summary>
    /// Groups the candidates with a usable hash by (size, hash) and gives every member of a set of more than
    /// one a full hash. Only candidates can be in such a set: its members share a size.
    /// </summary>
    private async Task CompleteFullHashesAsync(IReadOnlyList<ColumnarCatalogReader> readers, HashCandidate[] candidates)
    {
        _logger.LogDebug(string.Empty);
        _logger.LogDebug("Checking duplicates and completing full hash.");

        var hashed = new List<int>();
        for (var k = 0; k < candidates.Length; k++)
        {
            if (candidates[k].Flags.IsHashUsable()) hashed.Add(k);
        }

        // Descending size (largest hashed first), then hash, so each set is adjacent.
        hashed.Sort((x, y) =>
        {
            ref readonly var a = ref candidates[x];
            ref readonly var b = ref candidates[y];
            var cmp = b.Size.CompareTo(a.Size);
            if (cmp == 0) cmp = a.Hash.HashA.CompareTo(b.Hash.HashA);
            if (cmp == 0) cmp = a.Hash.HashB.CompareTo(b.Hash.HashB);
            return cmp != 0 ? cmp : x.CompareTo(y);
        });

        var entriesToHash = new List<int>();
        var sets = 0;
        var longest = 0;
        for (var s = 0; s < hashed.Count;)
        {
            var e = s + 1;
            while (e < hashed.Count && SameSizeAndHash(candidates[hashed[s]], candidates[hashed[e]])) e++;
            if (e - s > 1)
            {
                sets++;
                longest = Math.Max(longest, e - s);
                for (var k = s; k < e; k++) entriesToHash.Add(hashed[k]);
            }

            s = e;
        }

        ReportDuplicateSets(sets, entriesToHash.Count, longest);
        if (sets == 0)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cts.Token,
            MaxDegreeOfParallelism = 2
        };
        var breakReported = 0;

        try
        {
            await Parallel.ForEachAsync(entriesToHash, parallelOptions, async (c, _) =>
            {
                var flags = candidates[c].Flags;
                if (flags.IsHashUsable() && (flags & Flags.PartialHash) == 0)
                {
                    return; // already a full hash
                }

                var path = readers[candidates[c].Catalog].FullPath(candidates[c].Index);
                var hashResponse = await _hashHelper.GetHashResponseFromFile(path, null);
                if (hashResponse.IsSuccess)
                {
                    SetHash(ref candidates[c], hashResponse.Hash, hashResponse.IsPartialHash);
                    RecordFullHash(hashResponse);
                }
                else
                {
                    _duplicationStatistics.AddFailedToHash();
                }

                if (_cancellation.IsCancellationRequested)
                {
                    ReportBreakOnce(ref breakReported, FullPhaseBreakMessage);
                    await cts.CancelAsync();
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Full hash phase cancelled.");
        }
        catch (Exception ex)
        {
            RecordUnexpectedFailure(ex, nameof(CompleteFullHashesAsync));
        }
    }

    private static bool SameSizeAndHash(in HashCandidate a, in HashCandidate b) =>
        a.Size == b.Size && a.Hash.HashA == b.Hash.HashA && a.Hash.HashB == b.Hash.HashB;
}

/// <summary>
/// The flags and hashes <see cref="Duplication.ApplyHash(IReadOnlyList{ColumnarCatalogReader})"/> changed, per
/// catalog in reader order, sorted by entry index for <see cref="ColumnarFormat.WriteWithHashes"/>.
/// </summary>
public sealed class ColumnarHashUpdates
{
    private readonly EntryHashUpdate[][] _byCatalog;

    internal ColumnarHashUpdates(EntryHashUpdate[][] byCatalog) => _byCatalog = byCatalog;

    public ReadOnlySpan<EntryHashUpdate> For(int catalog) => _byCatalog[catalog];
}
