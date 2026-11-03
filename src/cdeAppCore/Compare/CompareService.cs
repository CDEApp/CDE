using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Compare;

public enum CompareRequestError
{
    /// <summary>Malformed input: too many pairs, an entry past the catalog, or a non-directory.</summary>
    BadRequest,

    /// <summary>A catalog id that is not loaded.</summary>
    NotFound,
}

/// <summary>The request cannot be answered; the API maps <see cref="Error"/> to a status code.</summary>
public sealed class CompareRequestException(CompareRequestError error, string message) : Exception(message)
{
    public CompareRequestError Error { get; } = error;
}

/// <summary>Exact subtree comparison of folder pairs, run where both catalogs are memory-mapped.</summary>
public interface ICompareService
{
    /// <summary>
    /// One summary per pair, in pair order. Walks both subtrees together and pairs children by folded name
    /// exactly as the Compare tab's <c>alignChildren</c> does.
    /// </summary>
    /// <exception cref="CompareRequestException">Too many pairs, an unknown catalog, an entry past its catalog, or a non-directory.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled mid-walk.</exception>
    IReadOnlyList<CompareSummaryDto> Summarize(IReadOnlyList<(EntryRefDto Left, EntryRefDto Right)> pairs,
        CancellationToken cancellationToken = default);
}

public sealed class CompareService : ICompareService
{
    public const int MaxPairs = 500;

    /// <summary>Catalogs from different scans round timestamps differently (FAT stores 2 s steps).</summary>
    public const long ModifiedToleranceMs = 2000;

    private const int CancelCheckInterval = 256;

    private readonly ICatalogSession _session;

    public CompareService(ICatalogSession session)
    {
        _session = session;
    }

    public IReadOnlyList<CompareSummaryDto> Summarize(IReadOnlyList<(EntryRefDto Left, EntryRefDto Right)> pairs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (pairs.Count > MaxPairs)
            throw new CompareRequestException(CompareRequestError.BadRequest, $"at most {MaxPairs} pairs per request");

        var catalogIds = new SortedSet<int>();
        foreach (var (left, right) in pairs)
        {
            catalogIds.Add(left.CatalogId);
            catalogIds.Add(right.CatalogId);
        }

        foreach (var id in catalogIds)
        {
            if (id < 0 || id >= _session.CatalogCount)
                throw new CompareRequestException(CompareRequestError.NotFound, $"no such catalog: {id}");
        }

        // One lease per distinct catalog, taken in ascending id order on this thread and held until the
        // walk ends: a rescan swap of any of them waits, and a pair inside one catalog never re-enters its lock.
        var leases = new List<CatalogReadLease>(catalogIds.Count);
        try
        {
            var sources = new Dictionary<int, IEntrySource>(catalogIds.Count);
            foreach (var id in catalogIds)
            {
                var lease = _session.AcquireRead(id);
                leases.Add(lease);
                sources[id] = lease.Source;
            }

            foreach (var (left, right) in pairs)
            {
                RequireDirectory(sources[left.CatalogId], left);
                RequireDirectory(sources[right.CatalogId], right);
            }

            var results = new CompareSummaryDto[pairs.Count];
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };
            Parallel.For(0, pairs.Count, options, () => new Walker(cancellationToken), (i, _, walker) =>
            {
                var (left, right) = pairs[i];
                results[i] = walker.Run(sources[left.CatalogId], left.EntryIndex, sources[right.CatalogId], right.EntryIndex);
                return walker;
            }, _ => { });
            return results;
        }
        finally
        {
            foreach (var lease in leases) lease.Dispose();
        }
    }

    private static void RequireDirectory(IEntrySource source, EntryRefDto entry)
    {
        if (entry.EntryIndex < 0 || entry.EntryIndex >= source.Count)
            throw new CompareRequestException(CompareRequestError.BadRequest,
                $"no such entry: {entry.CatalogId}-{entry.EntryIndex}");
        if (entry.EntryIndex != 0 && !source.IsDirectory(entry.EntryIndex)) // index 0 is the catalog root, always a folder
            throw new CompareRequestException(CompareRequestError.BadRequest,
                $"not a directory: {entry.CatalogId}-{entry.EntryIndex}");
    }

    /// <summary>
    /// Walks one folder pair with an explicit stack. Buffers are reused across pairs, so one walker serves
    /// one thread. Mirrors <c>alignChildren</c> / <c>bothSidesStatus</c> in the Compare tab's diffModel.ts.
    /// </summary>
    private sealed class Walker(CancellationToken ct)
    {
        private readonly Stack<(int Left, int Right)> _stack = new();
        private readonly List<int> _lefts = [];
        private readonly List<int> _rights = [];
        private readonly List<string> _leftNames = [];
        private readonly List<string> _rightNames = [];
        private readonly List<string> _leftFolded = [];
        private readonly List<string> _rightFolded = [];
        private readonly Dictionary<string, int> _groupOf = new(StringComparer.Ordinal);
        private int[] _leftGroup = [];
        private int[] _rightGroup = [];
        private int[] _leftStart = [];
        private int[] _rightStart = [];
        private int[] _leftOrder = [];
        private int[] _rightOrder = [];
        private bool[] _leftTaken = [];
        private bool[] _rightTaken = [];

        private IEntrySource _l;
        private IEntrySource _r;
        private CompareSummaryDto _sum;
        private int _sinceCheck;

        public CompareSummaryDto Run(IEntrySource left, int leftRoot, IEntrySource right, int rightRoot)
        {
            _l = left;
            _r = right;
            _sum = new CompareSummaryDto();
            ct.ThrowIfCancellationRequested();
            _stack.Clear();
            _stack.Push((leftRoot, rightRoot));
            while (_stack.Count > 0)
            {
                var (l, r) = _stack.Pop();
                Expand(l, r);
            }

            return _sum;
        }

        private void Expand(int leftDir, int rightDir)
        {
            _lefts.Clear();
            _rights.Clear();
            for (var c = _l.FirstChildOf(leftDir); c != EntryStore.None; c = _l.NextSiblingOf(c)) _lefts.Add(c);
            for (var c = _r.FirstChildOf(rightDir); c != EntryStore.None; c = _r.NextSiblingOf(c)) _rights.Add(c);

            Tick(_lefts.Count + _rights.Count);

            // One side empty: everything is one-sided, so no name is needed.
            if (_rights.Count == 0)
            {
                _sum.LeftOnly += _lefts.Count;
                return;
            }

            if (_lefts.Count == 0)
            {
                _sum.RightOnly += _rights.Count;
                return;
            }

            if (_lefts.Count == 1 && _rights.Count == 1)
            {
                var ln = _l.FullName(_lefts[0]);
                var rn = _r.FullName(_rights[0]);
                if (ln.ToLowerInvariant() == rn.ToLowerInvariant()) Pair(_lefts[0], _rights[0]);
                else
                {
                    _sum.LeftOnly++;
                    _sum.RightOnly++;
                }

                return;
            }

            AlignGeneral();
        }

        private void AlignGeneral()
        {
            _groupOf.Clear();
            _leftNames.Clear();
            _rightNames.Clear();
            _leftFolded.Clear();
            _rightFolded.Clear();
            Size(ref _leftGroup, _lefts.Count);
            Size(ref _rightGroup, _rights.Count);
            Size(ref _leftOrder, _lefts.Count);
            Size(ref _rightOrder, _rights.Count);
            Size(ref _leftTaken, _lefts.Count);
            Size(ref _rightTaken, _rights.Count);
            Array.Clear(_leftTaken, 0, _lefts.Count);
            Array.Clear(_rightTaken, 0, _rights.Count);

            for (var i = 0; i < _lefts.Count; i++)
            {
                var name = _l.FullName(_lefts[i]);
                _leftNames.Add(name);
                _leftGroup[i] = GroupId(name.ToLowerInvariant());
            }

            for (var i = 0; i < _rights.Count; i++)
            {
                var name = _r.FullName(_rights[i]);
                _rightNames.Add(name);
                _rightGroup[i] = GroupId(name.ToLowerInvariant());
            }

            var groups = _groupOf.Count;
            Size(ref _leftStart, groups + 1);
            Size(ref _rightStart, groups + 1);
            BucketByGroup(_leftGroup, _lefts.Count, groups, _leftStart, _leftOrder);
            BucketByGroup(_rightGroup, _rights.Count, groups, _rightStart, _rightOrder);

            for (var g = 0; g < groups; g++) PairGroup(_leftStart[g], _leftStart[g + 1], _rightStart[g], _rightStart[g + 1]);
        }

        private int GroupId(string folded)
        {
            if (_groupOf.TryGetValue(folded, out var id)) return id;
            id = _groupOf.Count;
            _groupOf.Add(folded, id);
            return id;
        }

        // Stable counting sort of entry positions by group, so each group keeps the folder's child order.
        private static void BucketByGroup(int[] group, int count, int groups, int[] start, int[] order)
        {
            Array.Clear(start, 0, groups + 1);
            for (var i = 0; i < count; i++) start[group[i] + 1]++;
            for (var g = 0; g < groups; g++) start[g + 1] += start[g];
            for (var i = 0; i < count; i++) order[start[group[i]]++] = i;
            for (var g = groups; g > 0; g--) start[g] = start[g - 1];
            start[0] = 0;
        }

        // Exact (ordinal) names pair first, then the rest in order; leftovers are one-sided.
        private void PairGroup(int leftFrom, int leftTo, int rightFrom, int rightTo)
        {
            for (var a = leftFrom; a < leftTo; a++)
            {
                var li = _leftOrder[a];
                for (var b = rightFrom; b < rightTo; b++)
                {
                    var ri = _rightOrder[b];
                    if (_rightTaken[ri] || !string.Equals(_leftNames[li], _rightNames[ri], StringComparison.Ordinal)) continue;
                    _leftTaken[li] = true;
                    _rightTaken[ri] = true;
                    Pair(_lefts[li], _rights[ri]);
                    break;
                }
            }

            var rb = rightFrom;
            for (var a = leftFrom; a < leftTo; a++)
            {
                var li = _leftOrder[a];
                if (_leftTaken[li]) continue;
                while (rb < rightTo && _rightTaken[_rightOrder[rb]]) rb++;
                if (rb < rightTo)
                {
                    var ri = _rightOrder[rb++];
                    _rightTaken[ri] = true;
                    Pair(_lefts[li], _rights[ri]);
                }
                else _sum.LeftOnly++;
            }

            for (; rb < rightTo; rb++)
            {
                if (!_rightTaken[_rightOrder[rb]]) _sum.RightOnly++;
            }
        }

        private void Pair(int l, int r)
        {
            var lDir = _l.IsDirectory(l);
            var rDir = _r.IsDirectory(r);
            if (lDir && rDir)
            {
                _stack.Push((l, r));
                return;
            }

            var lMs = ModifiedMs(_l, l);
            var rMs = ModifiedMs(_r, r);
            var comparable = lMs.HasValue && rMs.HasValue;
            var sameDate = comparable ? Math.Abs(lMs.Value - rMs.Value) <= ModifiedToleranceMs : lMs.HasValue == rMs.HasValue;
            if (!lDir && !rDir && _l.SizeOf(l) == _r.SizeOf(r) && sameDate)
            {
                _sum.SameFiles++;
                return;
            }

            _sum.DifferentFiles++;
            if (!comparable) return;
            var delta = lMs.Value - rMs.Value;
            if (delta > ModifiedToleranceMs) _sum.LeftNewerFiles++;
            else if (-delta > ModifiedToleranceMs) _sum.RightNewerFiles++;
        }

        // Same notion of a bad date as the listing's IsModifiedBad. Whole milliseconds, as the client's Date sees it.
        private static long? ModifiedMs(IEntrySource source, int i)
        {
            if ((source.FlagsOf(i) & Flags.ModifiedBad) == Flags.ModifiedBad) return null;
            return source.ModifiedTicksOf(i) / TimeSpan.TicksPerMillisecond;
        }

        private void Tick(int entries)
        {
            _sinceCheck += entries;
            if (_sinceCheck < CancelCheckInterval) return;
            _sinceCheck = 0;
            ct.ThrowIfCancellationRequested();
        }

        private static void Size<T>(ref T[] buffer, int length)
        {
            if (buffer.Length < length) buffer = new T[Math.Max(length, buffer.Length * 2)];
        }
    }
}
