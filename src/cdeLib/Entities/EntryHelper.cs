using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using cdeLib.Infrastructure;

namespace cdeLib.Entities;

public static class EntryHelper
{
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
        var parentPath = parentEntry.FullPath ?? "pnull";
        var childPath = dirEntry.Path ?? "dnull";

        // Use StringBuilder from pool for complex paths
        if (parentPath.Length + childPath.Length > 260) // MAX_PATH
        {
            var sb = PoolManager.GetStringBuilder();
            try
            {
                sb.Append(parentPath);
                if (!parentPath.EndsWith('\\') && !childPath.StartsWith('\\'))
                    sb.Append('\\');
                sb.Append(childPath);
                return sb.ToString();
            }
            finally
            {
                PoolManager.ReturnStringBuilder(sb);
            }
        }

        // Cache frequently accessed paths
        var cacheKey = $"{parentPath}|{childPath}";
        return PathCache.GetOrAdd(cacheKey, _ => System.IO.Path.Combine(parentPath, childPath));
    }

    private static readonly ConcurrentDictionary<string, string> PathCache = new(StringComparer.OrdinalIgnoreCase);


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