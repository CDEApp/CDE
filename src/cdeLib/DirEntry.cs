using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;

namespace cdeLib
{
    [DebuggerDisplay("Path = {Path} {Size}, Count = {Children != null ? Children.Count : 0} {IsHashDone} {Hash.HashB}")]
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
            PartialHash = 1 << 5,
            [Description("The Children are allready in default order.")]
            DefaultSort = 1 << 6
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

        public bool IsDefaultSort
        {
            get { return (BitFields & Flags.DefaultSort) == Flags.DefaultSort; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.DefaultSort;
                }
                else
                {
                    BitFields &= ~Flags.DefaultSort;
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

        public DirEntry(FileSystemEntryInfo fs) : this()
        {
            Path = fs.FileName;
            // TODO this assumes date error on LastModified, what about Created and LastAccessed ?
            try
            {
                Modified = fs.LastModified;
            }
            catch (ArgumentOutOfRangeException)
            {
                // AlphaFS blows up trying to convert bad DateTime. eg. 1/1/1601 
                // create a bad date time file copy file to NAS with date of 2098 it mucks up.
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

        // TODO these need to be centralised.
        public const CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
        public static readonly CompareInfo MyCompareInfo = CompareInfo.GetCompareInfo("en-US");

        public int SizeCompareWithDirTo(DirEntry de)
        {
            if (de == null)
            {
                return -1; // this before de
            }
            if (IsDirectory && !de.IsDirectory)
            {
                return -1; // this before de
            }
            if (!IsDirectory && de.IsDirectory)
            {
                return 1; // this after de
            }
            if (IsDirectory && de.IsDirectory)
            {   // sort by path if both dir's and sorting by Size ? maybe fill in size in field Hmm ? 
                // really cheap to calculate dir size.... i think i should fill it in ?
                return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
            }
            // the cast breaks this.
            return Size.CompareTo(de.Size);
        }

        public int ModifiedCompareTo(DirEntry de)
        {
            if (de == null)
            {
                return -1; // this before de
            }
            if (IsModifiedBad && !de.IsModifiedBad)
            {
                return -1; // this before de
            }
            if (!IsModifiedBad && de.IsModifiedBad)
            {
                return 1; // this after de
            }
            if (IsModifiedBad && de.IsModifiedBad)
            {
                return 0;
            }
            return DateTime.Compare(Modified, de.Modified);
        }

        // is this right ? for the simple compareResult invert we do in caller ? - maybe not ? keep dirs at top anyway ?
        public int PathCompareWithDirTo(DirEntry de)
        {
            if (de == null)
            {
                return -1; // this before de
            }
            if (IsDirectory && !de.IsDirectory)
            {
                return -1; // this before de
            }
            if (!IsDirectory && de.IsDirectory)
            {
                return 1; // this after de
            }
            return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
        }

        public int PathCompareTo(DirEntry de)
        {
            if (de == null)
            {
                return -1; // this before de
            }
            return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
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

    }
}