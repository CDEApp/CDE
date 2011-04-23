using System;
using System.Diagnostics;
using Alphaleonis.Win32.Filesystem;
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
        //[ProtoMember(5, IsRequired = true)]
        //public bool IsSymbolicLink { get; set; }
        //[ProtoMember(6, IsRequired = true)]
        //public bool IsReparsePoint { get; set; }

        public DirEntry() {}

        public DirEntry(FileSystemEntryInfo fs)
        {
            Name = fs.FileName;
            Modified = fs.LastModified;
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