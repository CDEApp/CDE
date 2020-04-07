using System.Collections.Generic;
using System.Linq;

namespace cdeLib
{
    public static class EntryHelper
    {
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
        /// <param name="catalogRootEntry">Catalog root entry, show we can bind the catalog name to each entry</param>
        public static void TraverseTreePair(IEnumerable<ICommonEntry> rootEntries, TraverseFunc traverseFunc,
            RootEntry catalogRootEntry = null)
        {
            if (traverseFunc == null)
            {
                return;
            } // nothing to do.

            var funcContinue = true;
            var dirs = new Stack<ICommonEntry>(rootEntries
                .Reverse()); // Reverse to keep same traversal order as prior code.

            while (funcContinue && dirs.Count > 0)
            {
                var commonEntry = dirs.Pop();
                if (commonEntry.Children == null)
                {
                    continue;
                } // empty directories may not have Children initialized.

                foreach (var dirEntry in commonEntry.Children)
                {
                    if (catalogRootEntry != null)
                    {
                        commonEntry.TheRootEntry = catalogRootEntry;
                    }

                    funcContinue = traverseFunc(commonEntry, dirEntry);
                    if (!funcContinue)
                    {
                        break;
                    }

                    if (dirEntry.IsDirectory)
                    {
                        dirs.Push(dirEntry);
                    }
                }
            }
        }
    }
}