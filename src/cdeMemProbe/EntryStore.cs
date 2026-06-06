using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeMemProbe;

/// <summary>
/// PROTOTYPE struct-of-arrays representation of a single catalog, to measure the memory a SoA model
/// would use versus the production pointer-based <see cref="DirEntry"/> tree. One slot per entry
/// across parallel arrays; the tree shape is encoded with int indices (firstChild / nextSibling /
/// parent) instead of object references — so there is NO per-entry object header and references
/// become 4-byte ints.
///
/// This is a measurement/feasibility prototype, not the production model. It deliberately keeps
/// names in a string[] (one ref per entry, exactly like today) so the comparison isolates the
/// STRUCTURAL saving (object headers + link fields), not name storage.
/// </summary>
public sealed class EntryStore
{
    public const int None = -1;

    public readonly int Count;

    // One slot per entry. Index 0 is the root.
    public readonly long[] ModifiedTicks;
    public readonly long[] Size;
    public readonly string[] Name;
    public readonly byte[] BitFields;
    public readonly int[] FirstChild;  // None if no children
    public readonly int[] NextSibling; // None if last sibling
    public readonly int[] Parent;      // None for the root

    // Hashes are allocated only when the catalog actually has them (the common load-to-search
    // catalog has none) — so an un-hashed catalog pays zero bytes here, unlike the inline 16-byte
    // Hash16 on every DirEntry today.
    public Hash16[] Hash;

    public string RootPath;

    private int _next;

    private EntryStore(int count)
    {
        Count = count;
        ModifiedTicks = new long[count];
        Size = new long[count];
        Name = new string[count];
        BitFields = new byte[count];
        FirstChild = new int[count];
        NextSibling = new int[count];
        Parent = new int[count];
    }

    public bool IsDirectory(int i) => ((Flags)BitFields[i] & Flags.Directory) == Flags.Directory;

    /// <summary>
    /// Build a SoA store from a loaded/generated catalog tree. The tree can be released afterwards;
    /// the store is self-contained.
    /// </summary>
    public static EntryStore Build(RootEntry root)
    {
        var count = checked((int)(root.FileEntryCount + root.DirEntryCount) + 1); // +1 for the root itself
        var store = new EntryStore(count) { RootPath = root.Path };

        var hashed = root.IsHashDone; // catalogs are hashed wholesale; cheap heuristic for the prototype
        if (hashed) store.Hash = new Hash16[count];

        var rootIdx = store._next++;
        store.Name[rootIdx] = root.Path;
        store.ModifiedTicks[rootIdx] = root.ModifiedTicks;
        store.Size[rootIdx] = root.Size;
        store.BitFields[rootIdx] = (byte)root.BitFields;
        store.Parent[rootIdx] = None;
        store.FirstChild[rootIdx] = None;
        store.NextSibling[rootIdx] = None;

        store.FillChildren(root.Children, rootIdx);
        return store;
    }

    private void FillChildren(IList<DirEntry> children, int parentIdx)
    {
        if (children == null) return;

        var prevSibling = None;
        foreach (var child in children)
        {
            var idx = _next++;
            Name[idx] = child.Path;
            ModifiedTicks[idx] = child.ModifiedTicks;
            Size[idx] = child.Size;
            BitFields[idx] = (byte)child.BitFields;
            Parent[idx] = parentIdx;
            FirstChild[idx] = None;
            NextSibling[idx] = None;
            if (Hash != null) Hash[idx] = child.Hash;

            if (prevSibling == None) FirstChild[parentIdx] = idx;
            else NextSibling[prevSibling] = idx;
            prevSibling = idx;

            if (child.IsDirectory) FillChildren(child.Children, idx);
        }
    }

    /// <summary>Full path of an entry, walking parent indices into a reused buffer (no per-node strings).</summary>
    public string FullPath(int i)
    {
        // Collect ancestors (leaf -> root) then write root-first.
        var stack = new Stack<int>();
        for (var cur = i; cur != None; cur = Parent[cur]) stack.Push(cur);
        var sb = new System.Text.StringBuilder(128);
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            if (sb.Length > 0 && sb[^1] != '\\' && sb[^1] != '/') sb.Append('\\');
            sb.Append(Name[idx]);
        }
        return sb.ToString();
    }

    /// <summary>Linear name-substring search over the flat arrays. Returns match count.</summary>
    public int CountNameMatches(string pattern)
    {
        var matches = 0;
        for (var i = 1; i < Count; i++) // skip root at 0
        {
            if (Name[i].Contains(pattern, StringComparison.OrdinalIgnoreCase)) matches++;
        }
        return matches;
    }
}
