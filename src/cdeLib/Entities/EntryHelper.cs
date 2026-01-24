using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using cdeLib.Infrastructure;

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

    public static string MakeFullPath(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        var a = parentEntry.FullPath ?? "pnull";
        var b = dirEntry.Path ?? "dnull";
        return System.IO.Path.Combine(a, b);
    }

    /// <summary>
    /// Creates a full path using a ThreadLocal StringBuilder to reduce allocations.
    /// Still allocates the final string, but avoids intermediate allocations from Path.Combine.
    /// </summary>
    public static string MakeFullPathPooled(ICommonEntry parentEntry, ICommonEntry dirEntry)
    {
        var sb = PathBuilder.Value!;
        sb.Clear();

        var parentPath = parentEntry.FullPath;
        if (parentPath != null)
        {
            sb.Append(parentPath);
            if (sb.Length > 0)
            {
                var lastChar = sb[sb.Length - 1];
                if (lastChar != '\\' && lastChar != '/')
                    sb.Append(System.IO.Path.DirectorySeparatorChar);
            }
        }

        sb.Append(dirEntry.Path ?? "dnull");
        return sb.ToString();
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

        // Add in reverse order without creating intermediate collection
        for (int i = rootArray.Length - 1; i >= 0; i--)
        {
            if (rootArray[i]?.Children != null)
                stack.Push(rootArray[i]);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var children = current.Children;

            if (children == null) continue;

            // Process children in batch to improve cache locality
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