using System;
using System.ComponentModel;
using FlatSharp.Attributes;
using ProtoBuf;

namespace cdeLib.Entities
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
    [FlatBufferTable]
    public class Entry : object
    {
        [ProtoMember(1, IsRequired = true)]
        [FlatBufferItem(1)]
        public virtual ulong Size { get; set; }
        [ProtoMember(2, IsRequired = true)]
        [FlatBufferItem(2)]
        public virtual DateTime Modified { get; set; }
        [ProtoMember(3, IsRequired = true)]
        [FlatBufferItem(3)]
        public virtual string Name { get; set; }
        [ProtoMember(4, IsRequired = true)]
        [FlatBufferItem(4)]
        public virtual string FullPath { get; set; }
        [ProtoMember(5, IsRequired = true)]
        [FlatBufferItem(5)]
        public virtual Hash16 Hash { get; set; } // waste 8 bytes with pointer if we dont store it here. this is 16bytes.

        [ProtoMember(6, IsRequired = true)]
        [FlatBufferItem(6)]
        public virtual int Child { get; set; }
        [ProtoMember(7, IsRequired = true)]
        [FlatBufferItem(7)]
        public virtual int Sibling { get; set; }
        [ProtoMember(8, IsRequired = true)]
        [FlatBufferItem(8)]
        public virtual int Parent { get; set; }

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

        [ProtoMember(9, IsRequired = true)]
        [FlatBufferItem(9)]
        public virtual Flags BitFields { get; set; }
        #region BitFields based properties
        public bool IsDirectory
        {
            get => (BitFields & Flags.Directory) == Flags.Directory;
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
            get => (BitFields & Flags.ModifiedBad) == Flags.ModifiedBad;
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
            get => (BitFields & Flags.SymbolicLink) == Flags.SymbolicLink;
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
            get => (BitFields & Flags.ReparsePoint) == Flags.ReparsePoint;
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
            get => (BitFields & Flags.HashDone) == Flags.HashDone;
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
            get => (BitFields & Flags.PartialHash) == Flags.PartialHash;
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
    }
}