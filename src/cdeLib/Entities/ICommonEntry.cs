using System;
using System.Collections.Generic;
using cdeLib.Infrastructure;

namespace cdeLib
{
    /// <summary>
    /// Returns true if want traversal to continue after this returns.
    /// </summary>
    public delegate bool TraverseFunc(ICommonEntry ce, ICommonEntry de);

    public interface ICommonEntry
    {
        bool IsDefaultSort { get; set; }
        int PathCompareWithDirTo(ICommonEntry de);
        RootEntry TheRootEntry { get; set; }
        List<ICommonEntry> Children { get; set; }
        long Size { get; set; }

        /// <summary>
        /// RootEntry this is the root path, DirEntry this is the entry name.
        /// </summary>
        string Path { get; set; }

        ICommonEntry ParentCommonEntry { get; set; }

        /// <summary>
        /// Populated on load, not saved to disk.
        /// </summary>
        string FullPath { get; set; }

        bool IsDirectory { get; set; }
        bool PathProblem { get; set; }
        long FileEntryCount { get; set; }
        long DirEntryCount { get; set; }
        DateTime Modified { get; set; }
        bool IsHashDone { get; set; }
        bool IsPartialHash { get; set; }
        Hash16 Hash { get; set; }

        void TraverseTreePair(TraverseFunc func);
        void TraverseTreesCopyHash(ICommonEntry destination);
        string MakeFullPath(ICommonEntry dirEntry);

        /// <summary>
        /// Return List of CommonEntry, first is RootEntry, rest are DirEntry that lead to this.
        /// </summary>
        List<ICommonEntry> GetListFromRoot();

        bool ExistsOnFileSystem();

        /// <summary>
        /// Is bad path
        /// </summary>
        /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
        bool IsBadPath();

        void SetSummaryFields();
    }
}