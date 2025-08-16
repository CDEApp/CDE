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

    public static IEnumerable<IPairDirEntry> GetPairDirEntries(IEnumerable<RootEntry> rootEntries)
    {
        return new PairDirEntryEnumerator(rootEntries);
    }
    
    public static IEnumerable<IPairDirEntry> GetPairDirEntriesPooled(IEnumerable<RootEntry> rootEntries)
    {
        return new PairDirEntryEnumerator(rootEntries, usePooling: true);
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
        if (traverseFunc == null)
        {
            // nothing to do.
            return;
        }

        var funcContinue = true;
        var rootEntryStack = new Stack<ICommonEntry>(rootEntries
            .Reverse()); // Reverse to keep same traversal order as prior code.

        while (funcContinue && rootEntryStack.Count > 0)
        {
            var rootEntry = rootEntryStack.Pop();

            // empty directories may not have Children initialized.
            if (rootEntry.Children == null)
            {
                continue;
            }

            foreach (var dirEntry in rootEntry.Children)
            {
                funcContinue = traverseFunc(rootEntry, dirEntry);
                if (!funcContinue)
                {
                    break;
                }

                if (dirEntry.IsDirectory)
                {
                    rootEntryStack.Push(dirEntry);
                }
            }
        }
    }
}