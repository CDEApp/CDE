using System;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;

namespace cdeLib
{
    // current direntry.. 
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
    public struct Entry
    {
        //public uint DirCount;
        //public uint FileCount;
        public ulong Size;
        public DateTime Modified;
        public string Name;
        public string FullPath;

        // lol one bit adds a big chunk of allocated structure size, but multibits cost no more.
        //public bool IsJunction;
        //public bool IsReparse;
        public Hash16 Hash; // waste 8 bytes with pointer if we dont store it here. this is 16bytes.

        public uint Child;
        public uint Sibling;
        public uint Parent;

        public const int Directory    = 0x0001;
        public const int Partialhash  = 0x0002;
        public const int BadModified  = 0x0004;
        public const int SymbolicLink = 0x0008;
        public const int ReparsePoint = 0x0010;

        public int BitFields;
        #region BitFields based properties
        public bool IsDirectory
        {
            get { return (BitFields & Directory) == Directory; }
            set
            {
                if (value)
                {
                    BitFields |= Directory;
                }
                else
                {
                    BitFields &= ~Directory;
                }
            }
        }

        public bool IsPartialHash
        {
            get { return (BitFields & Partialhash) == Partialhash; }
            set
            {
                if (value)
                {
                    BitFields |= Partialhash;
                }
                else
                {
                    BitFields &= ~Partialhash;
                }
            }
        }

        public bool IsBadModified
        {
            get { return (BitFields & BadModified) == BadModified; }
            set
            {
                if (value)
                {
                    BitFields |= BadModified;
                }
                else
                {
                    BitFields &= ~BadModified;
                }
            }
        }

        public bool IsSymbolicLink
        {
            get { return (BitFields & SymbolicLink) == SymbolicLink; }
            set
            {
                if (value)
                {
                    BitFields |= SymbolicLink;
                }
                else
                {
                    BitFields &= ~SymbolicLink;
                }
            }
        }

        public bool IsReparsePoint
        {
            get { return (BitFields & ReparsePoint) == ReparsePoint; }
            set
            {
                if (value)
                {
                    BitFields |= ReparsePoint;
                }
                else
                {
                    BitFields &= ~ReparsePoint;
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
            catch (ArgumentOutOfRangeException ex)
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

        public void SetParent(uint p)
        {
            Parent = p;
        }
    }
}