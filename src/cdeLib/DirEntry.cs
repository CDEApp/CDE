using System;
using System.Diagnostics;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;

namespace cdeLib
{
    [DebuggerDisplay("Name = {Name}, Count = {Children.Count}")]
    [ProtoContract]
    public class DirEntry : CommonEntry
    {
        [ProtoMember(2, IsRequired = true)]
        public string Name { get; set; }
        [ProtoMember(3, IsRequired = true)]
        public DateTime Modified { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public bool IsDirectory { get; set; }
        //[ProtoMember(?, IsRequired = true)]
        //public bool IsSymbolicLink { get; set; }
        //[ProtoMember(?, IsRequired = true)]
        //public bool IsReparsePoint { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public byte[] Hash { get; set; }

        public string HashAsString { get { return ByteArrayHelper.ByteArrayToString(Hash); } }

        [ProtoMember(6, IsRequired = true)]
        public bool IsPartialHash { get; set; }

        /// <summary>
        /// Populated on load, not saved to disk.
        /// </summary>
        //public string FullPath { get; set; }

        /// <summary>
        /// Use this key for finding duplicate files, it ensures that files of
        /// different length with same md5 wont compare as same via hash.
        /// </summary>
        public byte[] KeyHash
        {
            get
            {
                if (Hash != null)
                {
                    if (_workHash == null)
                    {
                        _workHash = Hash.Concat(BitConverter.GetBytes(Size)).ToArray();
                    }
                }
                return _workHash;
            }
        }
        private byte[] _workHash;

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
                Logger.Instance.LogException(ex,fs.FullPath);
            }
            IsDirectory = fs.IsDirectory;
            //IsSymbolicLink = fs.IsSymbolicLink;
            //IsReparsePoint = fs.IsReparsePoint;

            if (fs.IsDirectory)
            {
            }
            else
            {
                Size = (ulong)fs.FileSize;
            }

        //    Hash = new byte[16]; // everyone has a hash
        //    var DUMMY = 0x55FF55FFAADDEECCul;
        //    Hash[0] = (byte)(DUMMY & 0xFF);
        //    Hash[1] = (byte)(DUMMY >> 8 & 0xFF);
        //    Hash[2] = (byte)(DUMMY >> 16 & 0xFF);
        //    Hash[3] = (byte)(DUMMY >> 24 & 0xFF);
        //    Hash[4] = (byte)(DUMMY >> 32 & 0xFF);
        //    Hash[5] = (byte)(DUMMY >> 48 & 0xFF);
        //    Hash[6] = (byte)(DUMMY >> 56 & 0xFF);
        //    Hash[7] = (byte)(DUMMY & 0xFF);
        //    Hash[8] = (byte)(DUMMY >> 8 & 0xFF);
        //    Hash[9] = (byte)(DUMMY >> 16 & 0xFF);
        //    Hash[10] = (byte)(DUMMY >> 24 & 0xFF);
        //    Hash[11] = (byte)(DUMMY >> 32 & 0xFF);
        //    Hash[12] = (byte)(DUMMY >> 48 & 0xFF);
        //    Hash[13] = (byte)(DUMMY >> 56 & 0xFF);
        //    Hash[14] = (byte)(DUMMY & 0xFF);
        //    Hash[15] = (byte)(DUMMY & 0xFF);
        }
    }
}