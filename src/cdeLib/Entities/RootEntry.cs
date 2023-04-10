using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cdeLib.Extensions;
using cdeLib.Infrastructure.Config;
using cdeLib.IO;
using FlatSharp.Attributes;
using MessagePack;
using ProtoBuf;
using Serilog;

// ReSharper disable MemberCanBeProtected.Global

namespace cdeLib.Entities;

// TODO - RootEntry needs All the Flags.
// TODO - maybe RootEntry derives from DirEntry ? collapse CE and DE maybe ?
[DebuggerDisplay("Path = {Path}, Count = {Children.Count}")]
[ProtoContract]
[FlatBufferTable]
[MessagePackObject]
public class RootEntry : object, ICommonEntry
{
    private const string MatchAll = "*";
    private readonly IDriveInfoService _driveInfoService;

    [ProtoMember(2, IsRequired = true)]
    [FlatBufferItem(2)]
    [Key(2)]
    public virtual string Description { get; set; } // user entered description ?

    /// <summary>
    /// There are a standard set on C: drive in win7 do we care about them ? Filter em out ? or hold internal filter to filter em out ojn display optionally.
    /// </summary>
    [ProtoMember(3, IsRequired = true)]
    [FlatBufferItem(3)]
    [Key(3)]
    public virtual IList<string> PathsWithUnauthorisedExceptions { get; set; }

    [ProtoMember(4, IsRequired = true)]
    [FlatBufferItem(4)]
    [Key(4)]
    public virtual string DefaultFileName { get; set; }

    [ProtoMember(5, IsRequired = true)]
    [FlatBufferItem(5)]
    [Key(5)]
    public virtual string DriveLetterHint { get; set; }

    [ProtoMember(6, IsRequired = true)]
    [FlatBufferItem(6)]
    [Key(6)]
    public virtual long AvailSpace { get; set; }

    [ProtoMember(7, IsRequired = true)]
    [FlatBufferItem(7)]
    [Key(7)]
    public virtual long TotalSpace { get; set; }

    [IgnoreMember]
    public virtual DateTime ScanStartUTC
    {
        set => ScanStartUTCTicks = value.Ticks;
        get => DateTime.FromBinary(ScanStartUTCTicks);
    }

    [FlatBufferItem(8)]
    [ProtoMember(8, IsRequired = true)]
    [Key(8)]
    public virtual long ScanStartUTCTicks { get; set; }

    [IgnoreMember]
    public virtual DateTime ScanEndUTC
    {
        set => ScanEndUTCTicks = value.Ticks;
        get => DateTime.FromBinary(ScanEndUTCTicks);
    }

    [FlatBufferItem(9)]
    [ProtoMember(9, IsRequired = true)]
    [Key(9)]
    public virtual long ScanEndUTCTicks { get; set; }

    // [ProtoMember(10, IsRequired = true)] // need to save for new data model.
    // [FlatBufferItem(10)]
    // public virtual int RootIndex { get; set; } // hack with Entry and EntryStore

    [ProtoMember(11, IsRequired = true)] // hack to not load old files ?
    [FlatBufferItem(11)]
    [Key(11)]
    public virtual int Version { get; set; } = 3;

    [ProtoMember(18, IsRequired = false)]
    [FlatBufferItem(18)]
    [Key(18)]
    public virtual string VolumeName { get; set; }

    [IgnoreMember]
    public string ActualFileName { get; set; }

    [IgnoreMember]
    public double ScanDurationMilliseconds => (ScanEndUTC - ScanStartUTC).TotalMilliseconds;

    // ReSharper disable once MemberCanBePrivate.Global
    public RootEntry()
    {
        TheRootEntry = this;
        _driveInfoService = new DriveInfoService();
    }

    public RootEntry(IConfiguration configuration) : this()
    {
        EntryCountThreshold = configuration.ProgressUpdateInterval;
    }

    public void PopulateRoot(string startPath)
    {
        startPath = GetRootEntry(startPath);
        ScanStartUTC = DateTime.UtcNow;
        RecurseTree(startPath);
        ScanEndUTC = DateTime.UtcNow;
        SetInMemoryFields();
    }

    private string GetRootEntry(string startPath)
    {
        startPath = CanonicalPath(startPath);
        if (!Directory.Exists(startPath))
        {
            throw new ArgumentException($"Cannot find path \"{startPath}\"");
        }

        DefaultFileName = GetDefaultFileName(startPath, out var deviceHint, out _);
        Path = startPath;
        DriveLetterHint = deviceHint;

        var pathRoot = System.IO.Path.GetPathRoot(startPath);

        var driveInfo = _driveInfoService.GetDriveSpace(pathRoot);
        if (driveInfo.AvailableBytes != null) AvailSpace = driveInfo.AvailableBytes.Value;
        if (driveInfo.TotalBytes != null) TotalSpace = driveInfo.TotalBytes.Value;
        VolumeName = this.GetVolumeName(GetDirectoryRoot(pathRoot));
        return startPath;
    }

    public void SetInMemoryFields()
    {
        // Protobuf does not retain DateKind. So just handle it here
        ScanStartUTC = new DateTime(ScanStartUTC.Ticks, DateTimeKind.Utc);
        ScanEndUTC = new DateTime(ScanEndUTC.Ticks, DateTimeKind.Utc);
        FullPath = Path;
        SetCommonEntryFields();
        SetSummaryFields();
    }

    public string GetDefaultFileName(string scanPath, out string hint, out string volumeRoot)
    {
        const string ext = ".cde";
        string fileName;
        volumeRoot = GetDirectoryRoot(scanPath);
        var volumeName = GetVolumeName(volumeRoot);
        hint = GetDriverLetterHint(scanPath, volumeRoot);
        var filenameSafePath = SafeFileName(scanPath);
        if (IsUnc(scanPath))
        {
            fileName = $"{hint}-{filenameSafePath.Substring(2)}{ext}";
        }
        else
        {
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (volumeRoot == scanPath)
            {
                fileName = $"{hint}-{volumeName}{ext}";
            }
            else
            {
                fileName = $"{hint}-{volumeName}-{filenameSafePath}{ext}";
            }
        }

        return fileName;
    }

    #region Methods virtual to assist testing.

    // TODO may not be needed with move to dotnetcore3.0 and System.IO
    public virtual string GetFullPath(string path)
    {
        return System.IO.Path.GetFullPath(path);
    }

    public virtual bool IsUnc(string path)
    {
        return System.IO.Path.IsPathFullyQualified(path) && PathIsUnc(path);
    }

    public virtual string GetDirectoryRoot(string path)
    {
        return Directory.GetDirectoryRoot(path);
    }

    private bool PathIsUnc(string path)
    {
        return path.StartsWith("\\\\");
    }

    /// <summary>
    /// VolumeName is a windows specific thing.
    /// Ignored if path is unc.
    /// </summary>
    /// <param name="rootPath"></param>
    /// <returns>Volume Name or string.empty when can't</returns>
    public virtual string GetVolumeName(string rootPath)
    {
        if (PathIsUnc(rootPath))
        {
            Log.Logger.Verbose("Cannot obtain Volume Name of path {Path}", rootPath);
            return string.Empty;
        }

        var pathRoot = System.IO.Path.GetPathRoot(rootPath);
        if (string.IsNullOrEmpty(pathRoot))
            return string.Empty;
        try
        {
            var driveInfo = new DriveInfo(pathRoot);
            return driveInfo.VolumeLabel;
        }
        catch (ArgumentException ex)
        {
            Log.Logger.Warning(ex, "Error Getting Volume Name, its ok we'll continue");
            return string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// Return canonical version of path.
    /// Ensure device id are upper case.
    /// if ends in a '\' and its not just a device eg G:\ then strip trailing \
    /// </summary>
    public string CanonicalPath(string path)
    {
        path = GetFullPath(path); // Fully qualified path used to generate filename
        var volumeRoot = GetDirectoryRoot(path);
        if (IsUnc(path))
        {
            if (!System.IO.Path.EndsInDirectorySeparator(path))
            {
                path += System.IO.Path.DirectorySeparatorChar;
            }
        }
        else
        {
            if (System.IO.Path.EndsInDirectorySeparator(path) && volumeRoot != path)
            {
                path = path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            }

            path = char.ToUpper(path[0]) + path.Substring(1);
        }

        return path;
    }

    public string GetDriverLetterHint(string path, string volumeRoot)
    {
        return IsUnc(path) ? "UNC" : volumeRoot.Substring(0, 1);
    }

    /// <summary>
    /// Safe for windows which is the least forgiving filesystem I currently believe.
    /// </summary>
    /// <param name="path"></param>
    private static string SafeFileName(string path)
    {
        return path.Replace('\\', '_')
            .Replace('$', '_')
            .Replace(':', '_');
    }

    /// <summary>
    /// This version calls itself so it can cache the folders and the node in its own stack.
    /// This improves performance.
    /// </summary>
    public void RecurseTree(string startPath)
    {
        var entryCount = 0;
        var dirs = new Stack<(ICommonEntry, string)>();
        dirs.Push((this, startPath));
        while (dirs.Count > 0)
        {
            var (commonEntry, directory) = dirs.Pop();
            var dirInfo = new DirectoryInfo(directory);
            try
            {
                var fsInfos = dirInfo.EnumerateFileSystemInfos(MatchAll, SearchOption.TopDirectoryOnly);
                foreach (var fsInfo in fsInfos)
                {
                    var dirEntry = new DirEntry(fsInfo);
                    commonEntry.AddChild(dirEntry);
                    if (dirEntry.IsDirectory)
                    {
                        dirs.Push((dirEntry, fsInfo.FullName));
                    }

                    ++entryCount;
                    SimpleScanCountEvent?.Invoke(entryCount, fsInfo.FullName);

                    if (Hack.BreakConsoleFlag)
                    {
                        break;
                    }
                }

                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            }
            catch (UnauthorizedAccessException)
            {
                AddPathsWithUnauthorisedExceptions(directory);
            }
        }

        SimpleScanEndEvent?.Invoke();
    }

    private void AddPathsWithUnauthorisedExceptions(string directory)
    {
        PathsWithUnauthorisedExceptions ??= new List<string>();
        PathsWithUnauthorisedExceptions.Add(directory);
    }

    [IgnoreMember]
    public int EntryCountThreshold { get; set; }

    [IgnoreMember]
    public Action<int, string> SimpleScanCountEvent { get; set; }

    [IgnoreMember]
    public Action SimpleScanEndEvent { get; set; }

    [IgnoreMember]
    public Action<string, Exception> ExceptionEvent { get; set; }

    /// <summary>
    /// Set FullPath on all Directories.
    /// Set ParentCommonEntry on all Entries in tree with a parent.
    /// </summary>
    public void SetCommonEntryFields()
    {
        TraverseTreePair((p, d) =>
        {
            d.ParentCommonEntry = p;
            return true;
        });
    }

    /// <summary>
    /// Testing to see if this helps gc reuse mem on reload.
    /// </summary>
    public void ClearCommonEntryFields()
    {
        TraverseTreePair((p, d) =>
        {
            d.ParentCommonEntry = null;
            return true;
        });
    }

    public void SortAllChildrenByPath()
    {
        Children.Sort((de1, de2) => de1.PathCompareWithDirTo(de2)); // Sort root entries first.
        IsDefaultSort = true;

        TraverseTreePair((p, d) =>
        {
            if (d.IsDirectory && d.Children?.Count > 1)
            {
                d.Children.Sort((de1, de2) => de1.PathCompareWithDirTo(de2));
                d.IsDefaultSort = true;
            }

            return true;
        });
    }

    public int DescriptionCompareTo(RootEntry re, IConfigCdeLib config)
    {
        if (re == null)
        {
            return -1; // this before re
        }

        if (Description == null && re.Description != null)
        {
            return -1; // this before re
        }

        if (Description != null && re.Description == null)
        {
            return 1; // this after re
        }

        return config.CompareWithInfo(Description, re.Description);
    }

    // direntry import
    [IgnoreMember]
    public virtual DateTime Modified
    {
        set => ModifiedTicks = value.Ticks;
        get => DateTime.FromBinary(ModifiedTicks);
    }

    [ProtoMember(12, IsRequired = false)] // is there a better default value than 0 here
    [FlatBufferItem(12)]
    [Key(12)]
    public virtual Flags BitFields { get; set; }

    [ProtoMember(13, IsRequired = false)]
    [FlatBufferItem(13)]
    [Key(13)]
    public virtual Hash16 Hash { get; set; }

    [ProtoMember(14, IsRequired = true)]
    [FlatBufferItem(14)]
    [Key(14)]
    public virtual long ModifiedTicks { get; set; }

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
        Hash.SetHash(hash);
        IsHashDone = true;
    }

    // For testing convenience.
    public void SetHash(int hash)
    {
        Hash.HashB = (ulong) hash;
        IsHashDone = true;
    }

    public RootEntry(FileSystemInfo fs) : this()
    {
        Path = fs.Name;
        try
        {
            Modified = fs.LastWriteTime;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Log.Logger.Error(ex, "Error getting WriteTime for {Name}, using CreationTime instead", fs.Name);
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
        //{   // sort by path if both dirs and sorting by Size ? maybe fill in size in field Hmm ? 
        //    // really cheap to calculate dir size.... i think i should fill it in ?
        //    return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
        //}
        // the cast breaks this.
        var sizeCompare = Size.CompareTo(de.Size);
        return sizeCompare == 0 ? DirEntryConsts.MyCompareInfo.Compare(Path, de.Path, DirEntryConsts.MyCompareOptions) : sizeCompare;
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

    // can this be done with TraverseTree ?
    public void SetSummaryFields()
    {
        var size = 0L;
        var dirEntryCount = 0L;
        var fileEntryCount = 0L;
        PathProblem = IsBadPath();

        if (Children != null)
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
                else
                {
                    //dirEntry.PathProblem = dirEntry.IsBadPath();
                }

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
        return this;
    }

    [ProtoMember(15, IsRequired = false)]
    [FlatBufferItem(15)]
    [Key(15)]
    public virtual IList<DirEntry> Children { get; set; }

    public void AddChild(DirEntry child)
    {
        if (this.Children == null)
            Children = new List<DirEntry>();
        Children.Add(child);
    }

    [ProtoMember(16, IsRequired = true)]
    [FlatBufferItem(16)]
    [Key(16)]
    public virtual long Size { get; set; }

    /// <summary>
    /// RootEntry this is the root path, DirEntry this is the entry name.
    /// </summary>
    [ProtoMember(17, IsRequired = true)]
    [FlatBufferItem(17)]
    [Key(17)]
    public virtual string Path { get; set; }

    [IgnoreMember]
    public ICommonEntry ParentCommonEntry { get; set; }

    /// <summary>
    /// Populated on load, not saved to disk.
    /// </summary>
    [IgnoreMember]
    public string FullPath { get; set; }

    /// <summary>
    /// True if entry name ends with Space or Period which is a problem on windows file systems.
    /// If this entry is a directory this infects all child entries as well.
    /// Populated on load not saved to disk.
    /// </summary>
    [IgnoreMember]
    public bool PathProblem { get; set; }

    public void TraverseTreePair(TraverseFunc func)
    {
        TraverseTreePair(new List<ICommonEntry> {this}, func);
    }

    /// <summary>
    /// Recursive traversal
    /// </summary>
    /// <param name="rootEntries">Entries to traverse</param>
    /// <param name="traverseFunc">TraversalFunc</param>
    /// <param name="catalogRootEntry">Catalog root entry, show we can bind the catalog name to each entry</param>
    public static void TraverseTreePair(IEnumerable<ICommonEntry> rootEntries, TraverseFunc traverseFunc,
        RootEntry catalogRootEntry = null)
    {
        if (traverseFunc == null)
        {
            return;
        } // nothing to do.

        var funcContinue = true;
        var dirs = new Stack<ICommonEntry>(rootEntries
            .Reverse()); // Reverse to keep same traversal order as prior code.

        while (funcContinue && dirs.Count > 0)
        {
            var commonEntry = dirs.Pop();
            if (commonEntry.Children == null)
            {
                continue;
            } // empty directories may not have Children initialized.

            foreach (var dirEntry in commonEntry.Children)
            {
                funcContinue = traverseFunc(commonEntry, dirEntry);
                if (!funcContinue)
                {
                    break;
                }

                if (dirEntry.IsDirectory)
                {
                    dirs.Push(dirEntry);
                }
            }
        }
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
            var (workPath, baseSourceEntry, baseDestinationEntry) = dirs.Pop();

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
                            dirs.Push(Tuple.Create(fullPath, (ICommonEntry) sourceDirEntry,
                                (ICommonEntry) destinationDirEntry));
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

    /// <summary>
    /// Return List of CommonEntry, first is RootEntry, rest are DirEntry that lead to this.
    /// </summary>
    public IList<ICommonEntry> GetListFromRoot()
    {
        var activatedDirEntryList = new List<ICommonEntry>(8);
        for (var entry = this as ICommonEntry; entry != null; entry = entry.ParentCommonEntry)
        {
            activatedDirEntryList.Add(entry);
        }

        activatedDirEntryList.Reverse(); // list now from root to this.
        return activatedDirEntryList;
    }

    public bool ExistsOnFileSystem() => Directory.Exists(FullPath);

    /// <summary>
    /// Is bad path
    /// </summary>
    /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
    public bool IsBadPath()
    {
        // This probably needs to check all parent paths if this is a root entry.
        // Not high priority as will not generally be able to specify a folder with a problem path at or above root.
        return !string.IsNullOrEmpty(Path) && (Path.EndsWith(" ") || Path.EndsWith("."));
    }
}