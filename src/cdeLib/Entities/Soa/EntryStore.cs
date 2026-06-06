using System;
using System.Collections.Generic;
using System.Text;

namespace cdeLib.Entities.Soa;

/// <summary>
/// Struct-of-arrays representation of a single catalog: one slot per entry across parallel arrays,
/// with the tree shape encoded as <see cref="int"/> indices (firstChild / nextSibling / parent)
/// instead of object references. Holds the same catalog structure as a <see cref="RootEntry"/> tree
/// in roughly one third of the structural memory — no per-entry object header, and references shrink
/// from 8-byte pointers to 4-byte indices.
///
/// Phase 1 of the SoA migration: this is additive. <see cref="Build"/> converts an existing tree into
/// a store; the production tree model is unchanged. Later phases move load/search/serialization onto
/// the store and retire the tree.
///
/// Index 0 is always the catalog root. <see cref="None"/> (-1) terminates child/sibling chains and
/// marks the root's (absent) parent.
/// </summary>
public sealed class EntryStore
{
    public const int None = -1;

    public int Count { get; private set; }

    // One slot per entry (index 0 = root).
    public long[] ModifiedTicks { get; private set; }
    public long[] Size { get; private set; }

    // Names are stored split (name-without-extension + extension) reusing the SAME interned string
    // objects the source tree held — so conversion allocates no new name strings and the interned
    // originals are shared, not duplicated. FullName(i) rejoins on demand.
    public string[] Name { get; private set; }
    public string[] Ext { get; private set; }

    public byte[] BitFields { get; private set; }
    public int[] FirstChild { get; private set; }
    public int[] NextSibling { get; private set; }
    public int[] Parent { get; private set; }

    /// <summary>
    /// Content hashes, allocated lazily only when at least one entry is hashed. Null for the common
    /// un-hashed catalog (load-to-search), so an un-hashed store pays zero bytes here — unlike the
    /// inline 16-byte Hash16 carried by every DirEntry today.
    /// </summary>
    public Hash16[] Hash { get; private set; }

    private EntryStore(int count)
    {
        Count = count;
        ModifiedTicks = new long[count];
        Size = new long[count];
        Name = new string[count];
        Ext = new string[count];
        BitFields = new byte[count];
        FirstChild = new int[count];
        NextSibling = new int[count];
        Parent = new int[count];
    }

    /// <summary>Full entry name (name + extension), rejoined on demand like DirEntry.Path.</summary>
    public string FullName(int i) => string.IsNullOrEmpty(Ext[i]) ? Name[i] : string.Concat(Name[i], Ext[i]);

    public Flags Flags(int i) => (Flags)BitFields[i];
    public bool IsDirectory(int i) => (Flags(i) & Entities.Flags.Directory) == Entities.Flags.Directory;
    public bool IsHashDone(int i) => (Flags(i) & Entities.Flags.HashDone) == Entities.Flags.HashDone;
    public bool IsPartialHash(int i) => (Flags(i) & Entities.Flags.PartialHash) == Entities.Flags.PartialHash;
    public DateTime Modified(int i) => DateTime.FromBinary(ModifiedTicks[i]);

    /// <summary>Enumerate the direct child indices of <paramref name="parent"/> via the sibling chain.</summary>
    public IEnumerable<int> Children(int parent)
    {
        for (var c = FirstChild[parent]; c != None; c = NextSibling[c])
        {
            yield return c;
        }
    }

    /// <summary>
    /// Build the full path of entry <paramref name="i"/> into the supplied StringBuilder by walking
    /// parent indices (root-first). No intermediate per-node strings. Pass a reused builder on hot paths.
    /// </summary>
    public void AppendFullPath(StringBuilder sb, int i)
    {
        // Walk leaf -> root collecting indices, then emit root-first.
        var depth = 0;
        for (var cur = i; cur != None; cur = Parent[cur]) depth++;
        if (depth == 0) return;

        Span<int> chain = depth <= 64 ? stackalloc int[depth] : new int[depth];
        var n = 0;
        for (var cur = i; cur != None; cur = Parent[cur]) chain[n++] = cur;

        for (var k = depth - 1; k >= 0; k--)
        {
            var idx = chain[k];
            if (sb.Length > 0)
            {
                var last = sb[^1];
                if (last != '\\' && last != '/') sb.Append(System.IO.Path.DirectorySeparatorChar);
            }
            // Append the split name parts directly — no full-name string allocation for path building.
            sb.Append(Name[idx] ?? string.Empty);
            if (!string.IsNullOrEmpty(Ext[idx])) sb.Append(Ext[idx]);
        }
    }

    public string FullPath(int i)
    {
        var sb = new StringBuilder(128);
        AppendFullPath(sb, i);
        return sb.ToString();
    }

    /// <summary>
    /// Convert a loaded/generated catalog tree into a store. The tree may be released afterwards.
    /// Iterative (explicit stack) so very deep trees cannot overflow.
    /// </summary>
    public static EntryStore Build(RootEntry root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var count = CountEntries(root); // robust: counts the actual tree, not (possibly stale) summary fields
        var store = new EntryStore(count);

        var next = 0;
        var rootIdx = next++;
        store.Name[rootIdx] = root.Path; // root path is not split
        store.ModifiedTicks[rootIdx] = root.ModifiedTicks;
        store.Size[rootIdx] = root.Size;
        store.BitFields[rootIdx] = (byte)root.BitFields;
        store.Parent[rootIdx] = None;
        store.FirstChild[rootIdx] = None;
        store.NextSibling[rootIdx] = None;

        // Stack of (children-of-a-directory, that directory's index).
        var stack = new Stack<(IList<DirEntry> Children, int ParentIdx)>();
        stack.Push((root.Children, rootIdx));

        while (stack.Count > 0)
        {
            var (children, parentIdx) = stack.Pop();
            if (children == null) continue;

            var prevSibling = None;
            foreach (var child in children)
            {
                var idx = next++;
                // Reuse the child's already-interned name + extension objects (no fresh allocation).
                store.Name[idx] = child.NamePart;
                store.Ext[idx] = child.ExtPart;
                store.ModifiedTicks[idx] = child.ModifiedTicks;
                store.Size[idx] = child.Size;
                store.BitFields[idx] = (byte)child.BitFields;
                store.Parent[idx] = parentIdx;
                store.FirstChild[idx] = None;
                store.NextSibling[idx] = None;
                if (child.IsHashDone) store.SetHash(idx, child.Hash);

                if (prevSibling == None) store.FirstChild[parentIdx] = idx;
                else store.NextSibling[prevSibling] = idx;
                prevSibling = idx;

                if (child.IsDirectory) stack.Push((child.Children, idx));
            }
        }

        return store;
    }

    /// <summary>Count every entry in the tree (including the root) via an explicit stack.</summary>
    private static int CountEntries(RootEntry root)
    {
        var count = 1; // the root
        var stack = new Stack<IList<DirEntry>>();
        stack.Push(root.Children);
        while (stack.Count > 0)
        {
            var children = stack.Pop();
            if (children == null) continue;
            count += children.Count;
            foreach (var child in children)
            {
                if (child.IsDirectory) stack.Push(child.Children);
            }
        }
        return count;
    }

    private void SetHash(int i, Hash16 hash)
    {
        Hash ??= new Hash16[Count];
        Hash[i] = hash;
    }
}
