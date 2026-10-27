using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities.Soa;

namespace cdeLib.Entities.Columnar;

/// <summary>
/// Reuses content hashes from an existing <c>.cdex</c> when its path is rescanned. Walks the old
/// catalog's memory-mapped <see cref="ColumnarCatalogReader"/> by index against the freshly scanned
/// tree, so the old catalog is never rebuilt as a pointer tree.
///
/// Rules (identical to the former tree-to-tree <c>TraverseTreesCopyHash</c>): children are matched by
/// name (ordinal, ignoring case); a file's hash is copied only when size and modified time are equal
/// and the old entry has a hash; a full hash replaces a partial one, and a hash already present on
/// the new entry is kept unless the old one is full and the new one partial. Copying sets
/// <see cref="Flags.HashDone"/>.
/// </summary>
public static class ColumnarHashReuse
{
    private const int StackChars = 256;

    public static void CopyHashes(ColumnarCatalogReader source, RootEntry destination)
    {
        if (source == null || destination == null)
        {
            throw new ArgumentException("source and destination must be not null.");
        }

        if (!string.Equals(source.RootPath, destination.Path, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("source and destination must have same root path.");
        }

        var comparer = new NameComparer();
        var lookupSet = new HashSet<DirEntry>(comparer);
        var lookup = lookupSet.GetAlternateLookup<ReadOnlySpan<char>>();
        var largestSet = 0;

        var stack = new Stack<(int SourceIndex, IList<DirEntry> Destination)>(64);
        stack.Push((0, destination.Children));

        while (stack.Count > 0)
        {
            var (parentIdx, destChildren) = stack.Pop();
            var first = source.FirstChildOf(parentIdx);
            if (first == EntryStore.None || destChildren == null || destChildren.Count == 0)
            {
                continue;
            }

            // One lookup reused across directories; rebuilt only if a huge directory left it oversized
            // (HashSet.Clear is O(capacity)).
            if (largestSet > 4096 && destChildren.Count < largestSet / 16)
            {
                lookupSet = new HashSet<DirEntry>(comparer);
                lookup = lookupSet.GetAlternateLookup<ReadOnlySpan<char>>();
                largestSet = 0;
            }
            else
            {
                lookupSet.Clear();
            }

            largestSet = Math.Max(largestSet, destChildren.Count);
            foreach (var child in destChildren)
            {
                lookupSet.Add(child); // duplicate names: first one wins
            }

            for (var c = first; c != EntryStore.None; c = source.NextSiblingOf(c))
            {
                if (!TryFind(source, c, ref lookup, out var destChild))
                {
                    continue; // not present in the new scan
                }

                var sourceIsDir = source.IsDirectory(c);
                if (sourceIsDir && destChild.IsDirectory)
                {
                    stack.Push((c, destChild.Children));
                }
                else if (!sourceIsDir
                         && source.ModifiedTicksOf(c) == destChild.ModifiedTicks
                         && source.SizeOf(c) == destChild.Size)
                {
                    TryCopyHashIfBeneficial(source, c, destChild);
                }
            }
        }
    }

    private static bool TryFind(
        ColumnarCatalogReader source,
        int index,
        ref HashSet<DirEntry>.AlternateLookup<ReadOnlySpan<char>> lookup,
        out DirEntry found)
    {
        var utf8 = source.NameUtf8(index);
        char[] rented = null;
        Span<char> chars = utf8.Length <= StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(utf8.Length));
        try
        {
            var n = Encoding.UTF8.GetChars(utf8, chars);
            return lookup.TryGetValue(chars[..n], out found);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static void TryCopyHashIfBeneficial(ColumnarCatalogReader source, int index, DirEntry destination)
    {
        if (!source.IsHashDone(index))
        {
            return; // Source has no hash to copy
        }

        var sourcePartial = source.IsPartialHash(index);
        var shouldCopy = !destination.IsHashDone // Destination has no hash
                         || (!sourcePartial && destination.IsPartialHash); // Upgrading partial to full
        if (!shouldCopy)
        {
            return;
        }

        destination.IsPartialHash = sourcePartial;
        destination.Hash = source.HasHashes ? source.HashOf(index) : default;
        // IsHashDone is a separate BitFields flag; without it the copied hash is ignored by
        // hashing/serialization (the reused hash would be silently lost on the next save).
        destination.IsHashDone = true;
    }

    /// <summary>
    /// Case-insensitive ordinal comparison of a full entry name (name + extension) held split in a
    /// <see cref="DirEntry"/>, and of a span, without building the joined string.
    /// </summary>
    private sealed class NameComparer : IEqualityComparer<DirEntry>, IAlternateEqualityComparer<ReadOnlySpan<char>, DirEntry>
    {
        public bool Equals(DirEntry x, DirEntry y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            Span<char> bx = stackalloc char[StackChars];
            Span<char> by = stackalloc char[StackChars];
            return MemoryExtensions.Equals(Full(x, bx), Full(y, by), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(DirEntry obj)
        {
            Span<char> scratch = stackalloc char[StackChars];
            return string.GetHashCode(Full(obj, scratch), StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(ReadOnlySpan<char> alternate, DirEntry other)
        {
            Span<char> scratch = stackalloc char[StackChars];
            return MemoryExtensions.Equals(alternate, Full(other, scratch), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ReadOnlySpan<char> alternate)
            => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public DirEntry Create(ReadOnlySpan<char> alternate) => throw new NotSupportedException();

        // Joined name without allocating when it fits the scratch buffer (extensionless names never copy).
        private static ReadOnlySpan<char> Full(DirEntry e, Span<char> scratch)
        {
            var name = e.NamePart;
            var ext = e.ExtPart;
            if (string.IsNullOrEmpty(ext))
            {
                return name;
            }

            var total = name.Length + ext.Length;
            if (total > scratch.Length)
            {
                return e.Path; // very long name: allocate rather than truncate
            }

            name.CopyTo(scratch);
            ext.CopyTo(scratch[name.Length..]);
            return scratch[..total];
        }
    }
}
