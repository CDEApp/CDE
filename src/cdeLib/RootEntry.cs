using System;
using System.Collections.Generic;
using System.IO;
using cde;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib
{
    [ProtoContract]
    public class RootEntry : CommonEntry
    {
        const string MatchAll = "*";

        [ProtoMember(2, IsRequired = true)]
        public string RootPath { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public string VolumeName { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public string Description { get; set; }

        [ProtoMember(7, IsRequired = true)]
        public IList<DirEntry> List { get; set; }
        
        List<Type> exceptionList = new List<Type>
            {
                typeof (UnauthorizedAccessException)
            };

        /// <summary>
        /// This version recurses itself so it can cache the folders and the node in tree.
        /// This improves performance when building the tree enormously.
        /// </summary>
        public void RecurseTree(string startPath)
        {
            var dirs = new Stack<Tuple<CommonEntry,string>>();
            dirs.Push(Tuple.Create((CommonEntry)this, startPath));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var commonEntry = t.Item1;
                var directory = t.Item2;

                var fsEntries = Directory.GetFullFileSystemEntries
                    (null, directory, MatchAll, SearchOption.TopDirectoryOnly, false, null, exceptionList);
                foreach (var fsEntry in fsEntries)
                {
                    var dirEntry = new DirEntry(fsEntry);
                    commonEntry.Children.Add(dirEntry);
                    if (fsEntry.IsDirectory)
                    {
                        dirs.Push(Tuple.Create((CommonEntry)dirEntry, fsEntry.FullPath));
                    }
                }
            }
        }

        public CommonEntry FindDir(string basePath, string entryPath)
        {
            var relativePath = entryPath.GetRelativePath(basePath);
            if (relativePath == null)
            {
                throw new ArgumentException("Error entryPath must be logically under basePath.");
            }

            if (relativePath == string.Empty)
            {
                return this;
            }

            return FindClosestParentDir(relativePath);
        }

        public override void Write(Stream output)
        {
            Serializer.Serialize(output, this);
        }

        #region other implementations that are slower and or not worth using
        #if (LEFTOVERCODE)
        public bool RecurseTree1(string basePath)
        {
            var exceptionList = new List<Type>
                {
                    typeof (UnauthorizedAccessException)
                };

            var entries = Directory.GetFullFileSystemEntries
                (null, basePath, MatchAll, SearchOption.AllDirectories, false, null, exceptionList);
            foreach (var entry in entries)
            {
                FindDir(RootPath, entry.FullPath).Children.Add(new DirEntry(entry));
            }
            return true;
        }

        // version that manages recurse itself, so it can remember parent folders to build tree.
        // this is heaps quicker and covers all files, unlike theRecurseTree() above..
        //   dont understand why stuff is missing here ?
        public bool RecurseTree2(string basePath)
        {
            var dirs = new Stack<string>();
            dirs.Push(basePath);
            while (dirs.Count > 0)
            {
                var path = dirs.Pop();
                var ce = FindDir(RootPath, path);

                var entries = Directory.GetFullFileSystemEntries(path, MatchAll, SearchOption.TopDirectoryOnly);
                foreach (var entry in entries)
                {
                    ce.Children.Add(new DirEntry(entry));
                    if (entry.IsDirectory)
                    {
                        dirs.Push(entry.FullPath);
                    }
                }
            }
            return true;
        }

        public void BuildList(int startSize)
        {
            var exceptionList = new List<Type>
                {
                    typeof (UnauthorizedAccessException)
                };

            //var list = new List<string>(startSize);
            List = new List<DirEntry>(startSize);
            var entries = Directory.GetFullFileSystemEntries//(RootPath, MatchAll, SearchOption.AllDirectories);
                (null, RootPath, MatchAll, SearchOption.AllDirectories, false, null, exceptionList);
            foreach (var entry in entries)
            {
                //list.Add(entry.FullPath);
                List.Add(DirEntry.GetDirEntryFullPath(entry));
            }
            Console.WriteLine(" list " + List.Count);
        }
        #endif
        #endregion
    }
}