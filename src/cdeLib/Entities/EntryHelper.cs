using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace cdeLib.Entities;

public static class EntryHelper
{
    private static readonly ThreadLocal<StringBuilder> PathBuilder =
        new(() => new StringBuilder(512));

    public static IEnumerable<ICommonEntry> GetDirEntries(RootEntry rootEntry)
    {
        return new DirEntryEnumerator(rootEntry);
    }

    public static IEnumerable<ICommonEntry> GetDirEntries(IEnumerable<RootEntry> rootEntries)
    {
        return new DirEntryEnumerator(rootEntries);
    }

    public static IEnumerable<PairDirEntry> GetPairDirEntries(IEnumerable<RootEntry> rootEntries)
    {
        return new PairDirEntryEnumerator(rootEntries);
    }

    // Reused per-thread scratch buffer for allocation-free path matching (see FullPathContains).
    private static readonly ThreadLocal<char[]> PathMatchBuffer =
        new(() => new char[512]);

    /// <summary>
    /// Builds the full path of <paramref name="dirEntry"/> (whose directory parent is
    /// <paramref name="parentEntry"/>) into the shared ThreadLocal StringBuilder in a single pass,
    /// walking the parent chain via ParentCommonEntry. Avoids the recursive per-level full-path
    /// string allocations of the old approach (which called parent.FullPath at every level) — only
    /// each segment's name is materialised. Returns the StringBuilder for the caller to consume.
    /// </summary>
    private static StringBuilder BuildFullPath(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        var sb = PathBuilder.Value!;
        sb.Clear();

        AppendAncestorPath(sb, parentEntry);
        AppendSeparatorIfNeeded(sb);
        sb.Append(dirEntry.Path ?? "dnull");

        // Prevent the pooled StringBuilder from growing unbounded in long-running processes.
        if (sb.Capacity > 1024 && sb.Length <= 512)
        {
            sb.Capacity = 512;
        }

        return sb;
    }

    /// <summary>
    /// Append the full path of <paramref name="entry"/> to <paramref name="sb"/> root-first by
    /// recursing up the ParentCommonEntry chain. The root (null parent) contributes its stored
    /// FullPath; every descendant contributes its own Path segment. No intermediate path strings.
    /// </summary>
    private static void AppendAncestorPath(StringBuilder sb, ICommonEntry entry)
    {
        if (entry == null) return;

        var parent = entry.ParentCommonEntry;
        if (parent == null)
        {
            // Root entry: FullPath is a stored field on RootEntry (no recursion/allocation).
            sb.Append(entry.FullPath ?? entry.Path ?? string.Empty);
            return;
        }

        AppendAncestorPath(sb, parent);
        AppendSeparatorIfNeeded(sb);
        sb.Append(entry.Path ?? "dnull");
    }

    private static void AppendSeparatorIfNeeded(StringBuilder sb)
    {
        if (sb.Length == 0) return;
        var last = sb[^1];
        if (last != '\\' && last != '/')
            sb.Append(System.IO.Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Creates a full path using a ThreadLocal StringBuilder to reduce allocations.
    /// Still allocates the final string, but avoids intermediate allocations from Path.Combine
    /// and from recursive per-level full-path string building.
    /// </summary>
    public static string MakeFullPath(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        return BuildFullPath(parentEntry, dirEntry).ToString();
    }

    /// <summary>
    /// Alias for MakeFullPath (now uses pooled StringBuilder by default).
    /// Kept for backwards compatibility.
    /// </summary>
    public static string MakeFullPathPooled(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        return MakeFullPath(parentEntry, dirEntry);
    }

    /// <summary>
    /// Allocation-free full-path substring test: builds the full path into pooled buffers and
    /// matches <paramref name="pattern"/> over a span, without allocating the result string.
    /// Used by find's substring path search, the dominant allocator in path queries.
    /// </summary>
    public static bool FullPathContains(ICommonEntry parentEntry, ICommonEntry dirEntry,
        string pattern, StringComparison comparison)
    {
        var sb = BuildFullPath(parentEntry, dirEntry);
        var length = sb.Length;

        var buffer = PathMatchBuffer.Value!;
        if (buffer.Length < length)
        {
            buffer = new char[Math.Max(length, buffer.Length * 2)];
            PathMatchBuffer.Value = buffer;
        }

        sb.CopyTo(0, buffer, 0, length);
        return buffer.AsSpan(0, length).Contains(pattern, comparison);
    }

    /// <summary>
    /// Recursive traversal
    /// </summary>
    /// <param name="rootEntries">Entries to traverse</param>
    /// <param name="traverseFunc">TraversalFunc</param>
    public static void TraverseTreePair(IEnumerable<ICommonEntry> rootEntries, TraverseFunc traverseFunc)
    {
        if (traverseFunc == null) return;

        // Use array for better cache locality and avoid Reverse() allocation
        var rootArray = rootEntries as ICommonEntry[] ?? rootEntries.ToArray();

        // Pre-allocate stack with estimated capacity to reduce reallocations
        var estimatedCapacity = rootArray.Length * 8; // Heuristic based on typical tree depth
        var stack = new Stack<ICommonEntry>(estimatedCapacity);

        // Add in reverse order without creating an intermediate collection
        for (var i = rootArray.Length - 1; i >= 0; i--)
        {
            if (rootArray[i]?.Children != null)
                stack.Push(rootArray[i]);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var children = current.Children;

            if (children == null) continue;

            // Process children in a batch to improve cache locality
            foreach (var child in children)
            {
                if (!traverseFunc(current, child))
                    return; // Early termination - exit immediately

                // Only push directories with children to avoid unnecessary stack operations
                if (child.IsDirectory && child.Children?.Count > 0)
                {
                    stack.Push(child);
                }
            }
        }
    }

    /// <summary>
    /// Recursive traversal for a single root entry (optimized to avoid array allocation)
    /// </summary>
    /// <param name="rootEntry">Entry to traverse</param>
    /// <param name="traverseFunc">TraversalFunc</param>
    public static void TraverseTreePair(ICommonEntry rootEntry, TraverseFunc traverseFunc)
    {
        if (traverseFunc == null || rootEntry?.Children == null) return;

        // Estimate stack capacity based on typical tree depth (8 levels * avg branching)
        var stack = new Stack<ICommonEntry>(64);

        stack.Push(rootEntry);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var children = current.Children;

            if (children == null) continue;

            // Process children in a batch to improve cache locality
            foreach (var child in children)
            {
                if (!traverseFunc(current, child))
                    return; // Early termination - exit immediately

                // Only push directories with children to avoid unnecessary stack operations
                if (child.IsDirectory && child.Children?.Count > 0)
                {
                    stack.Push(child);
                }
            }
        }
    }
}