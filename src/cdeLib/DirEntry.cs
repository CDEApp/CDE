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
        private readonly ILogger _logger;

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
            _logger = new Logger();
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
                _logger.LogException(ex,fs.FullPath);
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
        }

    }
}