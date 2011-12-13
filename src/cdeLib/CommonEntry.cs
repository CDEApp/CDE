﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProtoBuf;
using Filesystem = Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    [ProtoContract
    ,ProtoInclude(1, typeof(RootEntry))
    ,ProtoInclude(2, typeof(DirEntry))]
    public abstract class CommonEntry
    {
        // ReSharper disable MemberCanBePrivate.Global
        [ProtoMember(3, IsRequired = false)]
        public List<DirEntry> Children { get; set; }
        // ReSharper restore MemberCanBePrivate.Global

        [ProtoMember(4, IsRequired = true)]
        public ulong Size { get; set; }

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

        [DebuggerDisplay("FileCount = {FileCount}, DirCount = {DirCount}")]
        public class DirStats
        {
            public uint DirCount;
            public uint FileCount;
        }

        // set DirCount FileCount DirSize
        // can this be done with TraverseTree ?
        public void SetSummaryFields(DirStats dirStats)
        {
            var size = 0ul;
            if (Children != null)   // now that empty directories may not have Children initialized.
            {
                foreach (var dirEntry in Children)
                {
                    if (dirEntry.IsDirectory)
                    {
                        dirEntry.SetSummaryFields(dirStats);
                        dirStats.DirCount += 1;
                    }
                    else
                    {
                        dirStats.FileCount += 1;
                    }
                    size += dirEntry.Size;
                }
            }
            Size = size;
        }

        public static void TraverseAllTrees(IEnumerable<RootEntry> rootEntries, Action<DirEntry> action)
        {
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.TraverseTree(action);
            }
        }

        // stripped down without fullpath carry along, see if it helps perf, it should some
        public void TraverseTree(string path, Action<DirEntry> action)
        {
            var dirs = new Stack<CommonEntry>();
            dirs.Push(this);

            while (dirs.Count > 0)
            {
                var commonEntry = dirs.Pop();

                if (commonEntry.Children != null) // now that empty directories may not have Children initialized.
                {
                    foreach (var dirEntry in commonEntry.Children)
                    {
                        if (action != null)
                        {
                            action(dirEntry);
                        }

                        if (dirEntry.IsDirectory)
                        {
                            dirs.Push(dirEntry);
                        }

                        if (Hack.BreakConsoleFlag)
                        {
                            Console.WriteLine("\nBreak key detected exiting full TraverseTree inner.");
                            break;
                        }
                    }
                }

                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine("\nBreak key detected exiting full TraverseTree outer.");
                    break;
                }
            }
        }

        public static void TraverseAllTreesPair(IEnumerable<RootEntry> rootEntries, Action<CommonEntry, DirEntry> action)
        {
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.TraverseTreePair(action);
            }
        }

        // stripped down without fullpath carry along, see if it helps perf, it should some
        public void TraverseTreePair(string path, Action<CommonEntry, DirEntry> action)
        {
            var dirs = new Stack<CommonEntry>();
            dirs.Push(this);

            while (dirs.Count > 0)
            {
                var commonEntry = dirs.Pop();

                if (commonEntry.Children != null)// now that empty directories may not have Children initialized.
                {
                    foreach (var dirEntry in commonEntry.Children)
                    {
                        if (action != null)
                        {
                            action(commonEntry, dirEntry);
                        }

                        if (dirEntry.IsDirectory)
                        {
                            dirs.Push(dirEntry);
                        }

                        if (Hack.BreakConsoleFlag)
                        {
                            Console.WriteLine("\nBreak key detected exiting full TraverseTree inner.");
                            break;
                        }
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

            var sourcePath = source.Path;
            var destinationPath = destination.Path;

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

                if (baseSourceEntry.Children != null)
                {
                    foreach (var sourceDirEntry in baseSourceEntry.Children)
                    {
                        var fullPath = Filesystem.Path.Combine(workPath, sourceDirEntry.Path);

                        // find if theres a destination entry available.
                        // size of dir is irrelevant. date of dir we don't care about.
                        var sourceEntry = sourceDirEntry;
                        var destinationDirEntry = baseDestinationEntry.Children.FirstOrDefault(
                            x => (x.Path == sourceEntry.Path));

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
            return Filesystem.Path.Combine(a, b);
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
    }
}


