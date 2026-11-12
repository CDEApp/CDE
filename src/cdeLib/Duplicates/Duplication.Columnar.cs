using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;

namespace cdeLib.Duplicates;

public partial class Duplication
{
    /// <summary>
    /// Index-based equivalent of <see cref="ApplyHash(IList{RootEntry})"/> that hashes straight from
    /// memory-mapped catalogs without rebuilding pointer trees (issue #40). The same files are hashed with the
    /// same two phases (first-pass hash of every file sharing its size with another, then a full hash of every
    /// set of equal size and first-pass hash), so the catalogs end with the same hashes and flags.
    /// The readers are not changed: the result holds each catalog's new flags and hashes for
    /// <see cref="ColumnarFormat.WriteWithHashes"/>. Hashes computed before a break are kept in the result.
    /// Managed memory grows with the number of files sharing a size, not with the total entry count.
    /// </summary>
    /// <param name="readers">The catalogs to hash.</param>
    /// <param name="selection">Which files to hash; null is <see cref="HashSelection.All"/>.</param>
    /// <param name="cancellationToken">Stops the run and throws <see cref="OperationCanceledException"/>, so no
    /// hashes are returned. A Ctrl-C break (<see cref="OperationCancellation"/>) instead ends a phase and keeps them.</param>
    public async Task<ColumnarHashUpdates> ApplyHash(IReadOnlyList<ColumnarCatalogReader> readers,
        HashSelection selection = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readers);
        selection ??= HashSelection.All;
        _logger.LogDebug("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var (candidates, grouping) = HashCandidateSelection.Select(readers, selection, cancellationToken);
        _logger.LogDebug("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        long totalFiles = 0;
        foreach (var r in readers) totalFiles += r.RootFileEntryCount;
        ReportSizeMatches(grouping.RepeatedSizes.Count, totalFiles, grouping.RepeatedFileCount, grouping.LongestRun,
            grouping.LongestRunSize);
        if (selection != HashSelection.All) ReportSelection(selection, candidates.Length);

        foreach (var r in readers)
        {
            if (r.Hashes == CatalogHashes.Legacy)
                ReportStatus($"{r.FilePath} has hashes from an older cde version; they are ignored and recomputed.");
        }

        // Largest files first, so a break part way still leaves the large files hashed; one group per
        // drive/share hashes in parallel with the others.
        var groupedByDriveRoot = GroupByDriveRoot(readers, candidates);

        long firstPassFiles = 0, firstPassBytes = 0;
        foreach (var c in candidates)
        {
            if (c.Flags.IsHashUsable()) continue;
            firstPassFiles++;
            firstPassBytes += FirstPassBytes(c.Size);
        }

        BeginPass(fullPass: false, firstPassFiles, firstPassBytes);

        var timer = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
        else await CompleteFullHashesAsync(readers, candidates, selection.IncludeUniqueSizes, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
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

        FileFinished(FirstPassBytes(candidates[c].Size));
    }

    private long FirstPassBytes(long size) => Math.Min(size, _configuration.HashFirstPassSize);

    private static bool HasFullHash(Flags flags) => flags.IsHashUsable() && (flags & Flags.PartialHash) == 0;

    private static void SetHash(ref HashCandidate candidate, Hash16 hash, bool partial)
    {
        candidate.Hash = hash;
        var flags = candidate.Flags | Flags.HashDone;
        candidate.Flags = partial
            ? flags | Flags.PartialHash | Flags.PartialHashV2
            : flags & ~(Flags.PartialHash | Flags.PartialHashV2);
    }

    /// <summary>
    /// Groups the candidates with a usable hash by (size, hash) and gives every member of a set of more than
    /// one a full hash. Only candidates can be in such a set: its members share a size.
    /// </summary>
    private async Task CompleteFullHashesAsync(IReadOnlyList<ColumnarCatalogReader> readers, HashCandidate[] candidates,
        bool fullHashEveryCandidate, CancellationToken cancellationToken)
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
        QueuePartialsBesideFullHashes(candidates, hashed, entriesToHash);
        if (fullHashEveryCandidate)
        {
            entriesToHash = hashed;
            _duplicationStatistics.FilesToCheckForDuplicatesCount = hashed.Count;
        }

        if (entriesToHash.Count == 0)
        {
            return;
        }

        long fullPassFiles = 0, fullPassBytes = 0;
        foreach (var c in entriesToHash)
        {
            if (HasFullHash(candidates[c].Flags)) continue;
            fullPassFiles++;
            fullPassBytes += candidates[c].Size;
        }

        if (fullPassFiles > 0) BeginPass(fullPass: true, fullPassFiles, fullPassBytes);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                if (HasFullHash(candidates[c].Flags))
                {
                    return;
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

                FileFinished(candidates[c].Size);

                if (_cancellation.IsCancellationRequested)
                {
                    ReportBreakOnce(ref breakReported, FullPhaseBreakMessage);
                    await cts.CancelAsync();
                }
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInfo("Full hash phase cancelled.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordUnexpectedFailure(ex, nameof(CompleteFullHashesAsync));
        }
    }

    /// <summary>
    /// A partial hash never equals a whole-file hash, so a file that only has a partial hash would never be compared with
    /// a same-size file that already has a full one (from an earlier promotion or an <c>--include-unique-sizes</c> run).
    /// Queue those partials for a full hash too. Files of exactly the same size are few unless they are copies.
    /// </summary>
    private static void QueuePartialsBesideFullHashes(HashCandidate[] candidates, List<int> hashed, List<int> entriesToHash)
    {
        HashSet<long> fullSizes = null;
        foreach (var k in hashed)
        {
            if ((candidates[k].Flags & Flags.PartialHash) == 0) (fullSizes ??= []).Add(candidates[k].Size);
        }

        if (fullSizes == null) return;
        var queued = new HashSet<int>(entriesToHash);
        foreach (var k in hashed)
        {
            if ((candidates[k].Flags & Flags.PartialHash) != 0 && fullSizes.Contains(candidates[k].Size) && queued.Add(k))
                entriesToHash.Add(k);
        }
    }

    private static bool SameSizeAndHash(in HashCandidate a, in HashCandidate b) =>
        a.Size == b.Size && a.Hash.HashA == b.Hash.HashA && a.Hash.HashB == b.Hash.HashB;
}

/// <summary>
/// The flags and hashes
/// <see cref="Duplication.ApplyHash(IReadOnlyList{ColumnarCatalogReader}, HashSelection, CancellationToken)"/> changed,
/// per catalog in reader order, sorted by entry index for <see cref="ColumnarFormat.WriteWithHashes"/>.
/// </summary>
public sealed class ColumnarHashUpdates
{
    private readonly EntryHashUpdate[][] _byCatalog;

    internal ColumnarHashUpdates(EntryHashUpdate[][] byCatalog) => _byCatalog = byCatalog;

    public ReadOnlySpan<EntryHashUpdate> For(int catalog) => _byCatalog[catalog];
}
