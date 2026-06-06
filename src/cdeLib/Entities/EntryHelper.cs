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

    /// <summary>
    /// Creates a full path using a ThreadLocal StringBuilder to reduce allocations.
    /// Still allocates the final string, but avoids intermediate allocations from Path.Combine.
    /// </summary>
    public static string MakeFullPath(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        // Resolve parent path FIRST — this may recursively re-enter MakeFullPath and
        // mutate the shared ThreadLocal StringBuilder, so do it before we clear/use sb.
        var parentPath = parentEntry.FullPath;

        var sb = PathBuilder.Value!;
        sb.Clear();

        if (parentPath != null)
        {
            sb.Append(parentPath);
            if (sb.Length > 0)
            {
                var lastChar = sb[^1];
                if (lastChar != '\\' && lastChar != '/')
                    sb.Append(System.IO.Path.DirectorySeparatorChar);
            }
        }

        sb.Append(dirEntry.Path ?? "dnull");
        var result = sb.ToString();

        // Prevent StringBuilder from growing unbounded in long-running processes
        // Only shrink if the capacity is large AND current content fits in target size
        if (sb.Capacity > 1024 && sb.Length <= 512)
        {
            sb.Capacity = 512;
        }

        return result;
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