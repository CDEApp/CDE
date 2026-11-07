using System;
using System.Collections.Generic;
using System.Threading;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Hashing;

namespace cdeLib.Duplicates;

internal struct HashCandidate
{
    public int Catalog;
    public int Index;
    public long Size;
    public Flags Flags;
    public Hash16 Hash;
}

internal static class HashCandidateSelection
{
    public sealed record Result(HashCandidate[] Candidates, ColumnarSizeGrouping.Result Grouping);

    public static Result Select(IReadOnlyList<ColumnarCatalogReader> readers, HashSelection selection,
        CancellationToken cancellationToken)
    {
        var grouping = ColumnarSizeGrouping.FindRepeatedSizes(readers, SizeCandidates.NonEmptyFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var capacityIsFinal = selection.Filter is null && !selection.IncludeUniqueSizes;
        var collector = new Collector(readers, grouping, selection.IncludeUniqueSizes,
            capacityIsFinal ? grouping.RepeatedFileCount : 0);
        var options = selection.Filter is null
            ? null
            : FindOptionsOverAllCatalogs(readers, selection.Filter, cancellationToken);
        for (var c = 0; c < readers.Count; c++)
        {
            collector.Catalog = c;
            if (options is null)
            {
                var r = readers[c];
                var bits = r.BitFields;
                var size = r.Size;
                for (var i = 1; i < r.Count; i++) collector.Add(i, bits[i], size[i]);
            }
            else
            {
                readers[c].Find(options, collector.Consider, () => cancellationToken.IsCancellationRequested);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var candidates = collector.ToArray();
        Array.Sort(candidates, static (a, b) =>
        {
            var bySize = b.Size.CompareTo(a.Size);
            if (bySize != 0) return bySize;
            var byCatalog = a.Catalog.CompareTo(b.Catalog);
            return byCatalog != 0 ? byCatalog : a.Index.CompareTo(b.Index);
        });
        return new Result(candidates, grouping);
    }

    private static EntryStoreFindOptions FindOptionsOverAllCatalogs(IReadOnlyList<ColumnarCatalogReader> readers,
        string filter, CancellationToken cancellationToken)
    {
        var specs = EverythingQuery.Parse(filter).DuplicateSpecs;
        return new EntryStoreFindOptions
        {
            Pattern = filter,
            EverythingMode = true,
            IncludeFolders = false,
            Duplicates = specs.Count > 0 ? DuplicateIndex.Build(specs, readers, cancellationToken) : null,
        };
    }

    private sealed class Collector(
        IReadOnlyList<ColumnarCatalogReader> readers,
        ColumnarSizeGrouping.Result grouping,
        bool includeUniqueSizes,
        long capacity)
    {
        private HashCandidate[] _buffer = new HashCandidate[capacity];
        private int _count;

        public int Catalog { get; set; }

        public void Consider(int i) => Add(i, readers[Catalog].Flags(i), readers[Catalog].SizeOf(i));

        public void Add(int i, Flags flags, long size)
        {
            if (!ColumnarSizeGrouping.IsCandidate(flags, size, SizeCandidates.NonEmptyFiles)) return;
            if (!includeUniqueSizes && !grouping.RepeatedSizes.Contains(size)) return;

            if (_count == _buffer.Length) Array.Resize(ref _buffer, Math.Max(16, _count * 2));
            _buffer[_count++] = new HashCandidate
            {
                Catalog = Catalog,
                Index = i,
                Size = size,
                Flags = flags,
                Hash = flags.IsHashUsable() ? readers[Catalog].HashOf(i) : default,
            };
        }

        public HashCandidate[] ToArray()
        {
            if (_count != _buffer.Length) Array.Resize(ref _buffer, _count);
            return _buffer;
        }
    }
}
