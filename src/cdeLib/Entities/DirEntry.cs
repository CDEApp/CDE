using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FlatSharp.Attributes;
using MessagePack;
using ProtoBuf;
using Serilog;

namespace cdeLib.Entities
{
    [DebuggerDisplay("Path = {Path} {Size}, Count = {Children != null ? Children.Count : 0} P{IsPartialHash} #{Hash.HashB}")]
    [ProtoContract]
    [FlatBufferTable]
    [MessagePackObject]
    public class DirEntry : ICommonEntry
    {
        [IgnoreMember]
        public virtual DateTime Modified
        {
            set => ModifiedTicks = value.Ticks;
            get => DateTime.FromBinary(ModifiedTicks);
        }

        [ProtoMember(1, IsRequired = true)]
        [FlatBufferItem(1)]
        [Key(1)]
        public virtual long ModifiedTicks { get; set; }

        [ProtoMember(2, IsRequired = false)]
        [FlatBufferItem(2)]
        [Key(2)]
        public virtual Hash16 Hash { get; set; }

        /// <summary>
        /// public bool ShouldSerializeHash() should be same as this, but isn't
        /// for current "protobuf-net r376local"
        /// URL some protobuf serialisation.
        /// http://stackoverflow.com/questions/6389477/how-to-add-optional-field-to-a-class-manually-in-protobuf-net
        /// </summary>
        [IgnoreMember]
        public bool HashSpecified => IsHashDone;

        //public string HashAsString { get { return ByteArrayHelper.ByteArrayToString(Hash); } }

        [ProtoMember(6, IsRequired = false)] // is there a better default value than 0 here
        [FlatBufferItem(6)]
        [Key(6)]
        public virtual Flags BitFields { get; set; }

        #region BitFields based properties

        [IgnoreMember]
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

        [IgnoreMember]
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

        [IgnoreMember]
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

        [IgnoreMember]
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

        [IgnoreMember]
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

        [IgnoreMember]
        public bool IsDefaultSort
        {
            get => (BitFields & Flags.DefaultSort) == Flags.DefaultSort;
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

        /// <summary>
        /// if this is a directory number of files contained in its hierarchy
        /// </summary>
        [IgnoreMember]
        public long FileEntryCount { get; set; }

        /// <summary>
        /// if this is a directory number of dirs contained in its hierarchy
        /// </summary>
        [IgnoreMember]
        public long DirEntryCount { get; set; }

        public void SetHash(byte[] hash)
        {
            Hash = new Hash16(hash);
            IsHashDone = true;
        }

        // For testing convenience.
        public void SetHash(int hash)
        {
            if (Hash == null)
            {
                Hash = new Hash16(hash);
            }
            else
            {
                Hash.HashB = (ulong) hash;
            }

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

        public void SetPath(string path)
        {
            this.Path = path;
            PathProblem = IsBadPath();
        }

        public DirEntry(FileSystemInfo fs) : this()
        {
            SetPath(fs.Name);
            try
            {
                Modified = fs.LastWriteTime;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Log.Logger.Error(ex, "Error getting WriteTime for {0}, using CreationTime instead.", fs.Name);
                Modified = fs.CreationTime;
            }

            IsDirectory = (fs.Attributes & FileAttributes.Directory) != 0;
            IsReparsePoint = (fs.Attributes & FileAttributes.ReparsePoint) != 0;

            if (fs is FileInfo info)
            {
                Size = info.Length;
            }
            else
            {
                Children = new List<DirEntry>();
            }
        }


        public int SizeCompareWithDirTo(ICommonEntry de)
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

            //if (IsDirectory && de.IsDirectory)
            //{   // sort by path if both dir's and sorting by Size ? maybe fill in size in field Hmm ? 
            //    // really cheap to calculate dir size.... i think i should fill it in ?
            //    return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
            //}
            // the cast breaks this.
            var sizeCompare = Size.CompareTo(de.Size);
            if (sizeCompare == 0)
            {
                return DirEntryConsts.MyCompareInfo.Compare(Path, de.Path, DirEntryConsts.MyCompareOptions);
            }

            return sizeCompare;
        }

        public int ModifiedCompareTo(ICommonEntry de)
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
        public int PathCompareWithDirTo(ICommonEntry de)
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

            return DirEntryConsts.MyCompareInfo.Compare(Path, de.Path, DirEntryConsts.MyCompareOptions);
        }

        public int PathCompareTo(ICommonEntry de)
        {
            if (de == null)
            {
                return -1; // this before de
            }

            return DirEntryConsts.MyCompareInfo.Compare(Path, de.Path, DirEntryConsts.MyCompareOptions);
        }

        // can this be done with TraverseTree ?
        public void SetSummaryFields()
        {
            var size = 0L;
            var dirEntryCount = 0L;
            var fileEntryCount = 0L;
            //PathProblem = IsBadPath();

            if (IsDirectory && Children != null)
            {
                var childrenDirEntryCount = 0L;
                foreach (var dirEntry in Children)
                {
                    if (dirEntry.IsDirectory)
                    {
                        dirEntry.SetSummaryFields();
                        if (PathProblem) // infects child entries
                        {
                            dirEntry.PathProblem = PathProblem;
                        }

                        ++dirEntryCount;
                    }

                    // else
                    // {
                    //     dirEntry.PathProblem = dirEntry.IsBadPath();
                    // }
                    size += dirEntry.Size;
                    fileEntryCount += dirEntry.FileEntryCount;
                    childrenDirEntryCount += dirEntry.DirEntryCount;
                }

                fileEntryCount += Children.Count - dirEntryCount;
                dirEntryCount += childrenDirEntryCount;
            }

            FileEntryCount = (uint) fileEntryCount;
            DirEntryCount = (uint) dirEntryCount;
            Size = size;
        }

        [IgnoreMember]
        public RootEntry TheRootEntry { get; set; }

        public RootEntry GetRootEntry()
        {
            ICommonEntry entry = this;
            while (entry.TheRootEntry == null)
            {
                entry = entry.ParentCommonEntry;
            }

            return entry.TheRootEntry;
        }

        // ReSharper disable MemberCanBePrivate.Global
        [ProtoMember(3, IsRequired = false)]
        [FlatBufferItem(3)]
        [Key(3)]
        public virtual IList<DirEntry> Children { get; set; }
        // ReSharper restore MemberCanBePrivate.Global

        public void AddChild(DirEntry child)
        {
            if (this.Children == null)
            {
                Children = new List<DirEntry>();
            }

            Children.Add(child);
        }

        [ProtoMember(4, IsRequired = true)]
        [FlatBufferItem(4)]
        [Key(4)]
        public virtual long Size { get; set; }

        /// <summary>
        /// RootEntry this is the root path, DirEntry this is the entry name.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        [FlatBufferItem(5)]
        [Key(5)]
        public virtual string Path { get; set; }

        [IgnoreMember]
        public ICommonEntry ParentCommonEntry { get; set; }

        /// <summary>
        /// Populated on load, not saved to disk.
        /// </summary>
        [IgnoreMember]
        //public string FullPath { get; set; }
        public string FullPath
        {
            get => ParentCommonEntry.MakeFullPath(this);
        }

        /// <summary>
        /// True if entry name ends with Space or Period which is a problem on windows file systems.
        /// If this entry is a directory this infects all child entries as well.
        /// Populated on load not saved to disk.
        /// </summary>
        [Key(7)]
        public bool PathProblem { get; set; } = false;

        public void TraverseTreePair(TraverseFunc func)
        {
            EntryHelper.TraverseTreePair(new List<ICommonEntry> {this}, func);
        }

        public void TraverseTreesCopyHash(ICommonEntry destination)
        {
            var dirs = new Stack<Tuple<string, ICommonEntry, ICommonEntry>>();
            var source = this;

            if (source == null || destination == null)
            {
                throw new ArgumentException("source and destination must be not null.");
            }

            var sourcePath = source.Path;
            var destinationPath = destination.Path;

            if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("source and destination must have same root path.");
            }

            // traverse every source entry copy across the meta data that matches on destination entry
            // if it adds value to destination.
            // if destination is not there source not processed.
            dirs.Push(Tuple.Create(sourcePath, (ICommonEntry) source, destination));

            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var workPath = t.Item1;
                var baseSourceEntry = t.Item2;
                var baseDestinationEntry = t.Item3;

                if (baseSourceEntry.Children != null)
                {
                    foreach (var sourceDirEntry in baseSourceEntry.Children)
                    {
                        var fullPath = System.IO.Path.Combine(workPath, sourceDirEntry.Path);

                        // find if there's a destination entry available.
                        // size of dir is irrelevant. date of dir we don't care about.
                        var sourceEntry = sourceDirEntry;
                        var destinationDirEntry = baseDestinationEntry.Children
                            .FirstOrDefault(x => x.Path == sourceEntry.Path);

                        if (destinationDirEntry == null)
                        {
                            continue;
                        }

                        if (!sourceDirEntry.IsDirectory
                            && sourceDirEntry.Modified == destinationDirEntry.Modified
                            && sourceDirEntry.Size == destinationDirEntry.Size)
                        {
                            // copy hash if none in destination.
                            // copy hash as upgrade to full if dest currently partial.
                            if (sourceDirEntry.IsHashDone
                                && !destinationDirEntry.IsHashDone
                                ||
                                sourceDirEntry.IsHashDone
                                && destinationDirEntry.IsHashDone
                                && !sourceDirEntry.IsPartialHash
                                && destinationDirEntry.IsPartialHash)
                            {
                                destinationDirEntry.IsPartialHash = sourceDirEntry.IsPartialHash;
                                destinationDirEntry.Hash = sourceDirEntry.Hash;
                            }
                        }
                        else
                        {
                            if (destinationDirEntry.IsDirectory)
                            {
                                dirs.Push(Tuple.Create(fullPath, (ICommonEntry) sourceDirEntry, (ICommonEntry) destinationDirEntry));
                            }
                        }
                    }
                }
            }
        }

        public string MakeFullPath(ICommonEntry dirEntry)
        {
            return EntryHelper.MakeFullPath(this, dirEntry);
        }

        public static IEnumerable<ICommonEntry> GetDirEntries(RootEntry rootEntry)
        {
            return new DirEntryEnumerator(rootEntry);
        }

        public static IEnumerable<ICommonEntry> GetDirEntries(IEnumerable<RootEntry> rootEntries)
        {
            return new DirEntryEnumerator(rootEntries);
        }

        public static IEnumerable<PairDirEntry> GetPairDirEntries(IEnumerable<RootEntry> rootEntries)
        {
            return new PairDirEntryEnumerator(rootEntries);
        }

        /// <summary>
        /// Return List of CommonEntry, first is RootEntry, rest are DirEntry that lead to this.
        /// </summary>
        public IList<ICommonEntry> GetListFromRoot()
        {
            var activatedDirEntryList = new List<ICommonEntry>(8);
            for (var entry = (ICommonEntry) this; entry != null; entry = entry.ParentCommonEntry)
            {
                activatedDirEntryList.Add(entry);
            }

            activatedDirEntryList.Reverse(); // list now from root to this.
            return activatedDirEntryList;
        }

        public bool ExistsOnFileSystem()
        {
            // CommonEntry is always a directory ? - not really.
            return Directory.Exists(FullPath);
        }

        /// <summary>
        /// Is bad path
        /// </summary>
        /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
        private bool IsBadPath()
        {
            // This probably needs to check all parent paths if this is a root entry.
            // Not high priority as will not generally be able to specify a folder with a problem path at or above root.
            return !string.IsNullOrEmpty(Path) && (Path.EndsWith(" ") || Path.EndsWith("."));
        }
    }
}