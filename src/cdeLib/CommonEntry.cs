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

        public uint FindEntries(string find, string path, FindEntryEvent fee)
        {
            var found = 0u;
            foreach (var dirEntry in Children)
            {
                var fullPath = Path.Combine(path, dirEntry.Name);
                if (dirEntry.Name.IndexOf(find) >= 0)
                {
                    ++found;
                    if (fee != null)
                    {
                        fee(fullPath, dirEntry);
                    }
                }
                if (dirEntry.IsDirectory)
                {
                    found += dirEntry.FindEntries(find, fullPath, fee);
                }
            }
            return found;
        }

        public delegate void FindEntryEvent(string path, DirEntry dirEntry);

    }
}