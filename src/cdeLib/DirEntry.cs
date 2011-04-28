using System;
using System.Diagnostics;
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
        //[ProtoMember(5, IsRequired = true)]
        //public bool IsSymbolicLink { get; set; }
        //[ProtoMember(6, IsRequired = true)]
        //public bool IsReparsePoint { get; set; }

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