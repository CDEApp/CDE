using System.Collections.Generic;
using System.Linq;

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
        var a = parentEntry.FullPath ?? "pnull";
        var b = dirEntry.Path ?? "dnull";
        return System.IO.Path.Combine(a, b);
    }

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