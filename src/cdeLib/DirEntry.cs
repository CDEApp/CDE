using System;
using System.Diagnostics;
using System.IO;
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
        public bool IsDir { get; set; }

        public DirEntry() {}

        public DirEntry(FileSystemEntryInfo fs)
        {
            Name = fs.FileName;
            Modified = fs.LastModified;
            if (fs.IsDirectory)
            {
                IsDir = fs.IsDirectory;
            }
            else
            {
                Size = (ulong)fs.FileSize;
            }
        }

        public static DirEntry GetDirEntryFullPath(FileSystemEntryInfo fs)
        {
            var de = new DirEntry();
            de.Name = fs.FullPath;
            de.Modified = fs.LastModified;
            if (fs.IsDirectory)
            {
                de.IsDir = fs.IsDirectory;
            }
            else
            {
                de.Size = (ulong)fs.FileSize;
            }
            return de;
        }

        public override void Write(Stream output)
        {
            Serializer.Serialize(output, this);
        }
    }
}