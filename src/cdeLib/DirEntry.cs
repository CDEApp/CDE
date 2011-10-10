﻿using System;
using System.Collections.Generic;
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
            [System.ComponentModel.Description("Obligatory none value.")]
            None = 0,
            [System.ComponentModel.Description("Is a directory.")]
            Directory = 1 << 0,
            [System.ComponentModel.Description("Has a bad modified date field.")]
            ModifiedBad = 1 << 1,
            [System.ComponentModel.Description("Is a symbolic link.")]
            SymbolicLink = 1 << 2,
            [System.ComponentModel.Description("Is a reparse point.")]
            ReparsePoint = 1 << 3,
            [System.ComponentModel.Description("Hashing was done for this.")]
            HashDone = 1 << 4,
            [System.ComponentModel.Description("The Hash if done was a partial.")]
            PartialHash = 1 << 5
        };

        [ProtoMember(2, IsRequired = true)]
        public string Name { get; set; }
        [ProtoMember(3, IsRequired = true)]
        public DateTime Modified { get; set; }

        [ProtoMember(4, IsRequired = false)]
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

        public DirEntry(FileSystemEntryInfo fs) : this()
        {
            Name = fs.FileName;
            try
            {
                Modified = fs.LastModified;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                //catch issue with crap date modified on some files. ie 1/1/1601 -- AlphaFS blows up.
                IsModifiedBad = true;
            }
            IsDirectory = fs.IsDirectory;
            IsSymbolicLink = fs.IsSymbolicLink;
            IsReparsePoint = fs.IsReparsePoint;
            if (!fs.IsDirectory)
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
    }
}