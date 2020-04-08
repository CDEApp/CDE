using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace cdeDataStructure3.Entities
{
    /// <summary>
    /// Returns true if want traversal to continue after this returns.
    /// </summary>
    public delegate bool TraverseFunc(CommonEntry ce, DirEntry de);

    [ProtoContract
     , ProtoInclude(1, typeof(RootEntry))
     , ProtoInclude(2, typeof(DirEntry))]
    public abstract class CommonEntry
    {
        public RootEntry RootEntry { get; set; }

        // ReSharper disable MemberCanBePrivate.Global
        [ProtoMember(3, IsRequired = false)]
        public List<DirEntry> Children { get; set; }
        // ReSharper restore MemberCanBePrivate.Global

        [ProtoMember(4, IsRequired = true)]
        public long Size { get; set; }

        /// <summary>
        /// RootEntry this is the root path, DirEntry this is the entry name.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        public string Path { get; set; }

        public CommonEntry ParentCommonEntry { get; set; }

        /// <summary>
        /// Populated on load, not saved to disk.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// True if entry name ends with Space or Period which is a problem on windows file systems.
        /// If this entry is a directory this infects all child entries as well.
        /// Populated on load not saved to disk.
        /// </summary>
        public bool PathProblem;


        public void TraverseTreePair(TraverseFunc func)
        {
            TraverseTreePair(new List<CommonEntry> {this}, func);
        }

        /// <summary>
        /// Recursive traversal
        /// </summary>
        /// <param name="rootEntries">Entries to traverse</param>
        /// <param name="traverseFunc">TraversalFunc</param>
        /// <param name="catalogRootEntry">Catalog root entry, show we can bind the catalog name to each entry</param>
        public static void TraverseTreePair(IEnumerable<CommonEntry> rootEntries, TraverseFunc traverseFunc,
            RootEntry catalogRootEntry = null)
        {
            if (traverseFunc == null)
            {
                return;
            } // nothing to do.

            var funcContinue = true;
            var dirs = new Stack<CommonEntry>(rootEntries
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
                        commonEntry.RootEntry = catalogRootEntry;
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

        public string MakeFullPath(DirEntry dirEntry)
        {
            return MakeFullPath(this, dirEntry);
        }

        public static string MakeFullPath(CommonEntry parentEntry, DirEntry dirEntry)
        {
            var a = parentEntry.FullPath ?? "pnull";
            var b = dirEntry.Path ?? "dnull";
            return System.IO.Path.Combine(a, b);
        }

        /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
        public bool IsBadPath()
        {
            // This probably needs to check all parent paths if this is a root entry.
            // Not high priority as will not generally be able to specify a folder with a problem path at or above root.
            return !string.IsNullOrEmpty(Path) && (Path.EndsWith(" ") || Path.EndsWith("."));
        }
    }
}