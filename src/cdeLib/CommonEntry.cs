using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace cdeLib
{
    [ProtoContract
    ,ProtoInclude(1, typeof(RootEntry))
    ,ProtoInclude(2, typeof(DirEntry))]
    public abstract class CommonEntry
    {
        [ProtoMember(3, IsRequired = true)]
        public ICollection<DirEntry> Children { get; set; }

        //[ProtoMember(4, IsRequired = true)] No longer saving set on load or scan.
        public uint DirCount { get; set; }

        //[ProtoMember(5, IsRequired = true)] No longer saving set on load or scan.
        public uint FileCount { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public ulong Size { get; set; }

        protected CommonEntry()
        {
            Children = new List<DirEntry>();    // only needed for dir's but who cares.
        }

        public CommonEntry FindClosestParentDir(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Argument relativePath must be non empty.");
            }
            var indexOfDirectorySeperator = relativePath.IndexOf(Path.DirectorySeparatorChar);
            var firstPathElement = relativePath;
            var remainderPath = string.Empty;
            if (indexOfDirectorySeperator > 0)
            {
                firstPathElement = relativePath.Remove(indexOfDirectorySeperator);
                remainderPath = relativePath.Substring(indexOfDirectorySeperator + Path.DirectorySeparatorChar.Length);
            }
            var de = Children.FirstOrDefault(x => x.Name == firstPathElement);
            if (de != null)
            {
                if (remainderPath == string.Empty)
                {
                    return de;
                }
                var foundDe = de.FindClosestParentDir(remainderPath);
                if (foundDe == null)
                {
                    return de;
                }
                return foundDe;
            }
            return this;
        }

        // set DirCount FileCount DirSize
        // can this be done with TraverseTree ?
        public void SetSummaryFields()
        {
            var fileCount = 0u;
            var dirCount = 0u;
            var size = 0ul;
            foreach (var dirEntry in Children)
            {
                if (dirEntry.IsDirectory)
                {
                    dirEntry.SetSummaryFields();
                    dirCount += dirEntry.DirCount + 1;
                    fileCount += dirEntry.FileCount;
                }
                else
                {
                    ++fileCount;
                }
                size += dirEntry.Size;
            }
            FileCount = fileCount;
            DirCount = dirCount;
            Size = size;
        }

        public static void TraverseAllTrees(IEnumerable<RootEntry> rootEntries, Action<string, DirEntry> action)
        {
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.TraverseTree(action);
            }
        }

        public void TraverseTree(string path, Action<string, DirEntry> action)
        {
            var dirs = new Stack<Tuple<CommonEntry, string>>();
            dirs.Push(Tuple.Create(this, path));

            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var commonEntry = t.Item1;
                var workPath = t.Item2;

                foreach (var dirEntry in commonEntry.Children)
                {
                    var fullPath = Path.Combine(workPath, dirEntry.Name);
                    if (action != null)
                    {
                        action(fullPath, dirEntry);
                    }

                    if (dirEntry.IsDirectory)
                    {
                        dirs.Push(Tuple.Create((CommonEntry)dirEntry, fullPath));
                    }

                    if (Hack.BreakConsoleFlag)
                    {
                        Console.WriteLine("\nBreak key detected exiting full TraverseTree inner.");
                        break;
                    }
                }

                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine("\nBreak key detected exiting full TraverseTree outer.");
                    break;
                }
            }
        }

        public void TraverseTreesCopyHash(RootEntry destination)
        {
            var dirs = new Stack<Tuple<string, CommonEntry, CommonEntry>>();
            var source = this as RootEntry;

            if (source == null || destination == null)
            {
                throw new ArgumentException("source and destination must be not null.");
            }

            var sourcePath = source.RootPath;
            var destinationPath = destination.RootPath;

            //if (sourcePath != destinationPath)
            if (string.Compare(sourcePath, destinationPath, true) != 0)
            {
                throw new ArgumentException("source and destination must have same root path.");
            }

            // traverse every source entry copy across the meta data that matches on destination entry
            // if it adds value to destination.
            // if destination is not there source not processed.
            dirs.Push(Tuple.Create(sourcePath, (CommonEntry) source, (CommonEntry) destination));

            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var workPath = t.Item1;
                var baseSourceEntry = t.Item2;
                var baseDestinationEntry = t.Item3;

                foreach (var sourceDirEntry in baseSourceEntry.Children)
                {
                    var fullPath = Path.Combine(workPath, sourceDirEntry.Name);

                    // find if theres a destination entry available.
                    // size of dir is irrelevant. date of dir we don't care about.
                    var sourceEntry = sourceDirEntry;
                    var destinationDirEntry = baseDestinationEntry.Children.FirstOrDefault(
                                                x => (x.Name == sourceEntry.Name));

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
                        if ((sourceDirEntry.Hash != null)
                             && (destinationDirEntry.Hash == null)
                            ||
                            ((sourceDirEntry.Hash != null)
                              && (destinationDirEntry.Hash != null)
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
    
        public static IEnumerable<FlatDirEntryDTO> GetDirEntries(List<RootEntry> rootEntries)
        {
            var entries = new Stack<FlatDirEntryDTO>();
            foreach (var re in rootEntries)
            {
                var rootPath = re.RootPath;

                foreach (var de in re.Children)
                {
                    var fullPath = Path.Combine(rootPath, de.Name);
                    entries.Push(new FlatDirEntryDTO(fullPath, de));
                }
            }

            while (entries.Count > 0)
            {
                var flatDe = entries.Pop();
                yield return flatDe;
            }
            yield break; // end of enum
        }
    }
}