using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace cdeLib
{
    /// <summary>
    /// Returns true if want traversal to continue after this returns.
    /// </summary>
    public delegate bool TraverseFunc(CommonEntry ce, DirEntry de);

    public delegate bool TraverseFuncWithRoot(CommonEntry ce, DirEntry de, RootEntry rootEntry = null);

    [ProtoContract
    ,ProtoInclude(1, typeof(RootEntry))
    ,ProtoInclude(2, typeof(DirEntry))]
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

        //public CommonEntry FindClosestParentDir(string relativePath)
        //{
        //    if (string.IsNullOrWhiteSpace(relativePath))
        //    {
        //        throw new ArgumentException("Argument relativePath must be non empty.");
        //    }
        //    var indexOfDirectorySeperator = relativePath.IndexOf(Filesystem.Path.DirectorySeparatorChar);
        //    var firstPathElement = relativePath;
        //    var remainderPath = string.Empty;
        //    if (indexOfDirectorySeperator > 0)
        //    {
        //        firstPathElement = relativePath.Remove(indexOfDirectorySeperator);
        //        remainderPath = relativePath.Substring(indexOfDirectorySeperator + Filesystem.Path.DirectorySeparatorChar.Length);
        //    }
        //    var de = Children.FirstOrDefault(x => x.Path == firstPathElement);
        //    if (de != null)
        //    {
        //        if (remainderPath == string.Empty)
        //        {
        //            return de;
        //        }
        //        var foundDe = de.FindClosestParentDir(remainderPath);
        //        if (foundDe == null)
        //        {
        //            return de;
        //        }
        //        return foundDe;
        //    }
        //    return this;
        //}

        public void TraverseTreePair(TraverseFunc func)
        {
            TraverseTreePair(new List<CommonEntry> { this }, func);
        }

        /// <summary>
        /// Recursive traversal
        /// </summary>
        /// <param name="rootEntries">Entries to traverse</param>
        /// <param name="traverseFunc">TraversalFunc</param>
        /// <param name="catalogRootEntry">Catalog root entry, show we can bind the catalog name to each entry</param>
        public static void TraverseTreePair(IEnumerable<CommonEntry> rootEntries, TraverseFunc traverseFunc, RootEntry catalogRootEntry = null)
        {
            if (traverseFunc == null) { return; } // nothing to do.

            var funcContinue = true;
            var dirs = new Stack<CommonEntry>(rootEntries.Reverse()); // Reverse to keep same traversal order as prior code.

            while (funcContinue && dirs.Count > 0)
            {
                var commonEntry = dirs.Pop();
                if (commonEntry.Children == null) { continue; } // empty directories may not have Children initialized.

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

        public void TraverseTreesCopyHash(CommonEntry destination)
        {
            var dirs = new Stack<Tuple<string, CommonEntry, CommonEntry>>();
            var source = this;

            if (source == null || destination == null)
            {
                throw new ArgumentException("source and destination must be not null.");
            }

            var sourcePath = source.Path;
            var destinationPath = destination.Path;

            if (string.Compare(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new ArgumentException("source and destination must have same root path.");
            }

            // traverse every source entry copy across the meta data that matches on destination entry
            // if it adds value to destination.
            // if destination is not there source not processed.
            dirs.Push(Tuple.Create(sourcePath, source, destination));

            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var workPath = t.Item1;
                var baseSourceEntry = t.Item2;
                var baseDestinationEntry = t.Item3;

                if (baseSourceEntry.Children != null)
                {
                    foreach (var sourceDirEntry in baseSourceEntry.Children)
                    {
                        var fullPath = System.IO.Path.Combine(workPath, sourceDirEntry.Path);

                        // find if there's a destination entry available.
                        // size of dir is irrelevant. date of dir we don't care about.
                        var sourceEntry = sourceDirEntry;
                        var destinationDirEntry = baseDestinationEntry.Children
                            .FirstOrDefault(x => (x.Path == sourceEntry.Path));

                        if (destinationDirEntry == null)
                        {
                            continue;
                        }

                        if (!sourceDirEntry.IsDirectory
                            && sourceDirEntry.Modified == destinationDirEntry.Modified
                            && sourceDirEntry.Size == destinationDirEntry.Size)
                        {
                            // copy MD5 if none in destination.
                            // copy MD5 as upgrade to full if dest currently partial.
                            if ((sourceDirEntry.IsHashDone)
                                && (!destinationDirEntry.IsHashDone)
                                ||
                                ((sourceDirEntry.IsHashDone)
                                 && (destinationDirEntry.IsHashDone)
                                 && !sourceDirEntry.IsPartialHash
                                 && destinationDirEntry.IsPartialHash
                                ))
                            {
                                destinationDirEntry.IsPartialHash = sourceDirEntry.IsPartialHash;
                                destinationDirEntry.Hash = sourceDirEntry.Hash;
                            }
                        }
                        else
                        {
                            if (destinationDirEntry.IsDirectory)
                            {
                                dirs.Push(Tuple.Create(fullPath, (CommonEntry) sourceDirEntry,
                                                       (CommonEntry) destinationDirEntry));
                            }
                        }
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

        public static IEnumerable<DirEntry> GetDirEntries(RootEntry rootEntry)
        {
            return new DirEntryEnumerator(rootEntry);
        }

        public static IEnumerable<DirEntry> GetDirEntries(IEnumerable<RootEntry> rootEntries)
        {
            return new DirEntryEnumerator(rootEntries);
        }

        public static IEnumerable<PairDirEntry> GetPairDirEntries(RootEntry rootEntry)
        {
            return new PairDirEntryEnumerator(rootEntry);
        }

        public static IEnumerable<PairDirEntry> GetPairDirEntries(IEnumerable<RootEntry> rootEntries)
        {
            return new PairDirEntryEnumerator(rootEntries);
        }

        /// <summary>
        /// Return List of CommonEntry, first is RootEntry, rest are DirEntry that lead to this.
        /// </summary>
        public List<CommonEntry> GetListFromRoot()
        {
            var activatedDirEntryList = new List<CommonEntry>(8);
            for (var entry = this; entry != null; entry = entry.ParentCommonEntry)
            {
                activatedDirEntryList.Add(entry);
            }
            activatedDirEntryList.Reverse(); // list now from root to this.
            return activatedDirEntryList;
        }

        public bool ExistsOnFileSystem()
        {   // CommonEntry is always a directory ? - not really.
            return System.IO.Directory.Exists(FullPath);
        }

        /// <summary>
        /// Is bad path
        /// </summary>
        /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
        public bool IsBadPath()
        {
            // This probably needs to check all parent paths if this is a root entry.
            // Not high priority as will not generally be able to specify a folder with a problem path at or above root.
            return !string.IsNullOrEmpty(Path) && (Path.EndsWith(" ") || Path.EndsWith("."));
        }
    }
}


