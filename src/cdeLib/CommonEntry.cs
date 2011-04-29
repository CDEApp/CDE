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

        [ProtoMember(4, IsRequired = true)]
        public uint DirCount { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public uint FileCount { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public ulong Size { get; set; }

        protected CommonEntry()
        {
            Children = new List<DirEntry>();    // only needed for dir's. todo optimise ?
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

        public void TraverseTree(string path, ApplyToEntry apply)
        {
            //var pathStack = new Stack<string>();
            //pathStack.Push(path);
            var dirs = new Stack<Tuple<CommonEntry, string>>();
            dirs.Push(Tuple.Create((CommonEntry)this, path));

            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var commonEntry = t.Item1;
                var workPath = t.Item2;

                //var workPath = pathStack.Pop();
                foreach (var dirEntry in commonEntry.Children)
                {
                    var fullPath = Path.Combine(workPath, dirEntry.Name);
                    if (apply != null)
                    {
                        apply(fullPath, dirEntry);
                    }

                    if (dirEntry.IsDirectory)
                    {
                        dirs.Push(Tuple.Create((CommonEntry)dirEntry, fullPath));
                    }
                }
            }
        }

        public delegate void ApplyToEntry(string fullPath, DirEntry dirEntry);

    }
}