using System;
using System.ComponentModel;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;

namespace cdeLib
{
    // C: drive Files 149,507 Dirs 24,690 Total Size of Files 26,825,007,368
    // FILE   9,623,653 no hashes.  cde --replfind  60.3 Meg, 32bit 
    // FILE  12,784,234 all hashes. cde --replfind  66.0 Meg, 32bit
    // FILE   9,623,653 no hashes.  cde --replfind  94.0 Meg, 64bit
    // FILE  12,784,234 all hashes. cde --replfind 100.3 Meg, 64bit
    //
    // FILE   4,110,553 no hashes, no paths, no names.  cde --replfind  38.9 Meg, 32bit
    //        173,747 @ 1million files = 38.9 * 5.75 = 223.67
    // FILE   4,110,553 no hashes, no paths, no names.  cde --replfind  62.8 Meg, 64bit
    //        173,747 @ 1million files = 62.8 * 5.75 = 361.1
    //
    // in array  1,000,000 is about  32Meg. 32bit -- old numbers before extra structure below.
    // in array 10,000,000 is about 320Meg. 32bit -- old numbers before extra structure below.
    //
    // in array  1,000,000 is about  64Meg. 32bit -- this excludes strings but includes hashes.
    // Adding--- the FullPath bit give 72Meg. raw data.
    //
    //
    // a million at an array does not seem bad.
    [ProtoContract]
    public struct Entry
    {
        [ProtoMember(1, IsRequired = true)]
        public ulong Size;
        [ProtoMember(2, IsRequired = true)]
        public DateTime Modified;
        [ProtoMember(3, IsRequired = true)]
        public string Name;
        [ProtoMember(4, IsRequired = true)]
        public string FullPath;
        [ProtoMember(5, IsRequired = true)]
        public Hash16 Hash; // waste 8 bytes with pointer if we dont store it here. this is 16bytes.

        [ProtoMember(6, IsRequired = true)]
        public int Child;
        [ProtoMember(7, IsRequired = true)]
        public int Sibling;
        [ProtoMember(8, IsRequired = true)]
        public int Parent;

        /// <summary>
        /// Entry flags.
        /// </summary>
        [Flags]
        public enum Flags
        {
            [Description("Obligatory none value.")]
            None = 0,
            [Description("Is a directory.")]
            Directory = 1 << 0,
            [Description("Has a bad modified date field.")]
            BadModified = 1 << 1,
            [Description("Is a symbolic link.")]
            SymbolicLink = 1 << 2,
            [Description("Is a reparse point.")]
            ReparsePoint = 1 << 3,
            [Description("Hashing was done for this.")]
            HashDone = 1 << 4,
            [Description("The Hash if done was a partial.")]
            PartialHash = 1 << 5
        };

        [ProtoMember(9, IsRequired = true)]
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

        public bool IsBadModified
        {
            get { return (BitFields & Flags.BadModified) == Flags.BadModified; }
            set
            {
                if (value)
                {
                    BitFields |= Flags.BadModified;
                }
                else
                {
                    BitFields &= ~Flags.BadModified;
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

        public void Set(FileSystemEntryInfo fs)
        {
            Name = fs.FileName;
            try
            {
                Modified = fs.LastModified;
            }
            catch (ArgumentOutOfRangeException)
            {
                //catch issue with crap date modified on some files. ie 1/1/1601 -- AlphaFS blows up.
                IsBadModified = true;
            }
            IsDirectory = fs.IsDirectory;
            IsSymbolicLink = fs.IsSymbolicLink;
            IsReparsePoint = fs.IsReparsePoint;
            if (!fs.IsDirectory)
            {
                Size = (ulong) fs.FileSize;
            }
        }
    }
}