using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;

namespace cdeLib
{
    [DebuggerDisplay("Name = {Name} {Size}, Count = {Children.Count} {IsHashDone} {Hash.HashB}")]
    [ProtoContract]
    public class DirEntry : CommonEntry
    {
        [Flags]
        public enum Flags
        {
            [Description("Obligatory none value.")]
            None = 0,
            [Description("Is a directory.")]
            Directory = 1 << 0,
            [Description("Has a bad modified date field.")]
            ModifiedBad = 1 << 1,
            [Description("Is a symbolic link.")]
            SymbolicLink = 1 << 2,
            [Description("Is a reparse point.")]
            ReparsePoint = 1 << 3,
            [Description("Hashing was done for this.")]
            HashDone = 1 << 4,
            [Description("The Hash if done was a partial.")]
            PartialHash = 1 << 5
        };

        [ProtoMember(1, IsRequired = true)]
        public DateTime Modified { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public Hash16 Hash;

        /// <summary>
        /// public bool ShouldSerializeHash() should be same as this, but isnt
        /// for current "protobuf-net r376local"
        /// URL some protobuf serialisation.
        /// http://stackoverflow.com/questions/6389477/how-to-add-optional-field-to-a-class-manually-in-protobuf-net
        /// </summary>
        public bool HashSpecified
        {
            get { return IsHashDone; }
        }

        //public string HashAsString { get { return ByteArrayHelper.ByteArrayToString(Hash); } }

        [ProtoMember(5, IsRequired = false)] // is there a better default value than 0 here
        public Flags BitFields;

        #region BitFields based properties
        public bool IsDirectory
        {
            get { return (BitFields & Flags.Directory) == Flags.Directory; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.Directory;
                }
                else
                {
                    BitFields &= ~Flags.Directory;
                }
            }
        }

        public bool IsModifiedBad
        {
            get { return (BitFields & Flags.ModifiedBad) == Flags.ModifiedBad; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.ModifiedBad;
                }
                else
                {
                    BitFields &= ~Flags.ModifiedBad;
                }
            }
        }

        public bool IsSymbolicLink
        {
            get { return (BitFields & Flags.SymbolicLink) == Flags.SymbolicLink; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.SymbolicLink;
                }
                else
                {
                    BitFields &= ~Flags.SymbolicLink;
                }
            }
        }

        public bool IsReparsePoint
        {
            get { return (BitFields & Flags.ReparsePoint) == Flags.ReparsePoint; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.ReparsePoint;
                }
                else
                {
                    BitFields &= ~Flags.ReparsePoint;
                }
            }
        }

        public bool IsHashDone
        {
            get { return (BitFields & Flags.HashDone) == Flags.HashDone; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.HashDone;
                }
                else
                {
                    BitFields &= ~Flags.HashDone;
                }
            }
        }

        public bool IsPartialHash
        {
            get { return (BitFields & Flags.PartialHash) == Flags.PartialHash; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.PartialHash;
                }
                else
                {
                    BitFields &= ~Flags.PartialHash;
                }
            }
        }
        #endregion

        public void SetHash(byte[] hash)
        {
            Hash.SetHash(hash);
            IsHashDone = true;
        }

        // For testing convenience.
        public void SetHash(int hash)
        {
            Hash.HashB = (ulong)hash;
            IsHashDone = true;
        }

        public DirEntry()
        {
        }

        /// <summary>
        /// For Testing.
        /// </summary>
        public DirEntry(bool isDirectory)
        {
            IsDirectory = isDirectory;
            if (isDirectory)
            {
                Children = new List<DirEntry>();
            }
        }

        override public bool IsRoot()
        {
            return false;
        }

        public DirEntry(FileSystemEntryInfo fs) : this()
        {
            Path = fs.FileName;
            // TODO this assumes date error on LastModified, what about Created and LastAccessed ?
            try
            {
                Modified = fs.LastModified;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // AlphaFS blows up trying to convert bad DateTime. eg. 1/1/1601 
                IsModifiedBad = true;
            }
            IsDirectory = fs.IsDirectory;
            IsSymbolicLink = fs.IsSymbolicLink;
            IsReparsePoint = fs.IsReparsePoint;
            if (IsDirectory)
            {
                Children = new List<DirEntry>();
            }
            else
            {
                Size = (ulong)fs.FileSize;
            }
        }

        public class EqualityComparer : IEqualityComparer<DirEntry>
        {
            public bool Equals(DirEntry x, DirEntry y)
            {
                return StaticEquals(x, y);
            }

            public int GetHashCode(DirEntry obj)
            {
                return StaticGetHashCode(obj);
            }

            public static bool StaticEquals(DirEntry x, DirEntry y)
            {
                if (x == null || y == null
                    || !x.IsHashDone || !y.IsHashDone)
                {
                    return false;
                }
                return Hash16.EqualityComparer.StaticEquals(x.Hash, y.Hash)
                    && x.Size == y.Size;
            }

            public static int StaticGetHashCode(DirEntry obj)
            {
                // quite likely a bad choice for hash.
                // if Hash not set then. avoid using it...
                if (obj.IsHashDone)
                {
                    return (Hash16.EqualityComparer.StaticGetHashCode(obj.Hash) * 31 +
                       (int)(obj.Size >> 32)) * 31 +
                       (int)(obj.Size & 0xFFFFFFFF);
                }

                return (int)(obj.Size >> 32) * 31 +
                       (int)(obj.Size & 0xFFFFFFFF);
            }
        }

        /// <returns>List of CommonEntry, first is RootEntry, rest are DirEntry</returns>
        public static IEnumerable<CommonEntry> GetListFromRoot(CommonEntry dirEntry)
        {
            var activatedDirEntryList = new List<CommonEntry>(10) {dirEntry};
            // every item in list view has a parent, a highest level possibe it is a RootEntry
            var parentCommonEntry = dirEntry.ParentCommonEntry;
            while (parentCommonEntry.ParentCommonEntry != null)
            {
                activatedDirEntryList.Add(parentCommonEntry);
                parentCommonEntry = parentCommonEntry.ParentCommonEntry;
            }
            activatedDirEntryList.Add(parentCommonEntry);
            activatedDirEntryList.Reverse(); // list now contains entries leading from root to our activated DirEntry
            return activatedDirEntryList;
        }
    }
}