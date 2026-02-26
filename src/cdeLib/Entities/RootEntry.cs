using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cdeLib.Extensions;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.IO;
using FlatSharp.Attributes;
using MessagePack;
using ProtoBuf;
using Serilog;

namespace cdeLib.Entities;

[DebuggerDisplay("Path = {Path}, Count = {Children.Count}")]
[ProtoContract]
[FlatBufferTable]
[MessagePackObject]
public sealed class RootEntry : object, ICommonEntry
{
    private const string MatchAll = "*";
    private readonly IDriveInfoService _driveInfoService;
    private readonly IFileSystemAdapter _fileSystemAdapter;

    [ProtoMember(2, IsRequired = true)]
    [FlatBufferItem(2)]
    [Key(2)]
    public string Description { get; set; } // user entered description ?

    /// <summary>
    /// There are a standard set on C: drive in win7 do we care about them? Filter em out ? or hold internal filter to filter em out ojn display optionally.
    /// </summary>
    [ProtoMember(3, IsRequired = true)]
    [FlatBufferItem(3)]
    [Key(3)]
    public IList<string> PathsWithUnauthorisedExceptions { get; set; }

    [ProtoMember(4, IsRequired = true)]
    [FlatBufferItem(4)]
    [Key(4)]
    public string DefaultFileName { get; set; }

    [ProtoMember(5, IsRequired = true)]
    [FlatBufferItem(5)]
    [Key(5)]
    public string DriveLetterHint { get; set; }

    [ProtoMember(6, IsRequired = true)]
    [FlatBufferItem(6)]
    [Key(6)]
    public long AvailSpace { get; set; }

    [ProtoMember(7, IsRequired = true)]
    [FlatBufferItem(7)]
    [Key(7)]
    public long TotalSpace { get; set; }

    [IgnoreMember]
    public DateTime ScanStartUtc
    {
        set => ScanStartUtcTicks = value.Ticks;
        get => DateTime.FromBinary(ScanStartUtcTicks);
    }

    [FlatBufferItem(8)]
    [ProtoMember(8, IsRequired = true)]
    [Key(8)]
    public long ScanStartUtcTicks { get; set; }

    [IgnoreMember]
    public DateTime ScanEndUtc
    {
        set => ScanEndUtcTicks = value.Ticks;
        get => DateTime.FromBinary(ScanEndUtcTicks);
    }

    [FlatBufferItem(9)]
    [ProtoMember(9, IsRequired = true)]
    [Key(9)]
    public long ScanEndUtcTicks { get; set; }

    // [ProtoMember(10, IsRequired = true)] // need to save for new data model.
    // [FlatBufferItem(10)]
    // public virtual int RootIndex { get; set; } // hack with Entry and EntryStore

    [ProtoMember(11, IsRequired = true)] // hack to not load old files ?
    [FlatBufferItem(11)]
    [Key(11)]
    public int Version { get; set; } = 3;

    [ProtoMember(18, IsRequired = false)]
    [FlatBufferItem(18)]
    [Key(18)]
    public string VolumeName { get; set; }

    [IgnoreMember]
    public string ActualFileName { get; set; }

    [IgnoreMember]
    public double ScanDurationMilliseconds => (ScanEndUtc - ScanStartUtc).TotalMilliseconds;

    public RootEntry() : this(null, null)
    {
    }

    public RootEntry(IConfiguration configuration) : this(configuration, null)
    {
    }

    public RootEntry(IConfiguration configuration, IFileSystemAdapter fileSystemAdapter)
    {
        TheRootEntry = this;
        _driveInfoService = new DriveInfoService();
        _fileSystemAdapter = fileSystemAdapter ?? new FileSystemAdapter();
        if (configuration != null)
        {
            EntryCountThreshold = configuration.ProgressUpdateInterval;
        }
    }

    public void PopulateRoot(string startPath)
    {
        startPath = GetRootEntry(startPath);
        ScanStartUtc = DateTime.UtcNow;
        RecurseTree(startPath);
        ScanEndUtc = DateTime.UtcNow;
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
        ScanStartUtc = new DateTime(ScanStartUtc.Ticks, DateTimeKind.Utc);
        ScanEndUtc = new DateTime(ScanEndUtc.Ticks, DateTimeKind.Utc);
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
            // Use Span slicing instead of Substring to avoid allocation
            fileName = $"{hint}-{filenameSafePath.AsSpan(2)}{ext}";
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

    #region File system operations (delegated to adapter for testability)

    private string GetFullPath(string path)
    {
        return _fileSystemAdapter.GetFullPath(path);
    }

    private bool IsUnc(string path)
    {
        return _fileSystemAdapter.IsUnc(path);
    }

    private string GetDirectoryRoot(string path)
    {
        return _fileSystemAdapter.GetDirectoryRoot(path);
    }

    /// <summary>
    /// VolumeName is a windows-specific thing.
    /// Ignored if path is unc.
    /// </summary>
    /// <param name="rootPath"></param>
    /// <returns>Volume Name or string.empty when can't</returns>
    public string GetVolumeName(string rootPath)
    {
        if (IsUnc(rootPath))
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
    /// Return a canonical version of a path.
    /// Ensure device id are upper case.
    /// if ends in a '\' and it's not just a device eg G:\ then strip trailing \
    /// </summary>
    public string CanonicalPath(string path)
    {
        path = GetFullPath(path); // Fully qualified path used to generate a filename
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

            path = char.ToUpper(path[0]) + path[1..];
        }

        return path;
    }

    public string GetDriverLetterHint(string path, string volumeRoot)
    {
        // Use Span slicing instead of Substring to avoid allocation
        return IsUnc(path) ? "UNC" : new string(volumeRoot.AsSpan(0, 1));
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
    /// Iteratively scans directory tree using a stack-based approach for optimal performance.
    /// </summary>
    public void RecurseTree(string startPath)
    {
        var entryCount = 0;
        var stack = new Stack<(ICommonEntry, string)>(capacity: 64);
        stack.Push((this, startPath));

        var progressTracker = new ScanProgressTracker(SimpleScanCountEvent);

        while (stack.Count > 0)
        {
            var (parent, directory) = stack.Pop();

            if (TryEnumerateDirectory(directory, parent, stack, ref entryCount, progressTracker))
            {
                continue;
            }

            if (Hack.BreakConsoleFlag)
            {
                break;
            }
        }

        progressTracker.ReportFinalCount(entryCount, startPath);
        SimpleScanEndEvent?.Invoke();
    }

    /// <summary>
    /// Attempts to enumerate a directory and add its children. Returns true on success, false if access denied.
    /// </summary>
    private bool TryEnumerateDirectory(
        string directory,
        ICommonEntry parent,
        Stack<(ICommonEntry, string)> stack,
        ref int entryCount,
        ScanProgressTracker progressTracker)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directory);
            var fsInfos = dirInfo.EnumerateFileSystemInfos(MatchAll, SearchOption.TopDirectoryOnly);

            foreach (var fsInfo in fsInfos)
            {
                ProcessFileSystemEntry(fsInfo, parent, stack, ref entryCount, directory, progressTracker);

                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            }

            return true;
        }
        catch (Exception ex) when (IsFileSystemAccessException(ex))
        {
            HandleFileSystemAccessError(ex, directory);
            return false;
        }
    }

    /// <summary>
    /// Processes a single file system entry, creates a DirEntry, and pushes directories to the stack.
    /// </summary>
    private void ProcessFileSystemEntry(
        FileSystemInfo fsInfo,
        ICommonEntry parent,
        Stack<(ICommonEntry, string)> stack,
        ref int entryCount,
        string currentDirectory,
        ScanProgressTracker progressTracker)
    {
        var dirEntry = new DirEntry(fsInfo);
        parent.AddChild(dirEntry);

        if (dirEntry.IsDirectory)
        {
            stack.Push((dirEntry, fsInfo.FullName));
        }

        entryCount++;
        progressTracker.ReportProgress(entryCount, currentDirectory);
    }

    /// <summary>
    /// Determines if an exception is a file system access error that should be logged and skipped.
    /// </summary>
    private static bool IsFileSystemAccessException(Exception ex)
    {
        return ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException or PathTooLongException;
    }

    /// <summary>
    /// Handles file system access errors by logging and tracking unauthorized paths.
    /// </summary>
    private void HandleFileSystemAccessError(Exception ex, string directory)
    {
        var logMessage = ex switch
        {
            UnauthorizedAccessException => "Access denied: {Path} - {Message}",
            IOException => "Cannot access: {Path} - {Message}",
            _ => "Skipping: {Path} - {Message}"
        };

        Log.Logger.Warning(logMessage, directory, ex.Message);
        AddPathsWithUnauthorisedExceptions(directory);
    }

    /// <summary>
    /// Manages progress event reporting with batching to reduce overhead.
    /// </summary>
    private sealed class ScanProgressTracker
    {
        private const int EventBatchSize = 1000;
        private readonly Action<int, string> _eventHandler;
        private int _nextEventThreshold;

        public ScanProgressTracker(Action<int, string> eventHandler)
        {
            _eventHandler = eventHandler;
            _nextEventThreshold = EventBatchSize;
        }

        public void ReportProgress(int entryCount, string currentDirectory)
        {
            if (_eventHandler != null && entryCount >= _nextEventThreshold)
            {
                _eventHandler(entryCount, currentDirectory);
                _nextEventThreshold += EventBatchSize;
            }
        }

        public void ReportFinalCount(int entryCount, string startPath)
        {
            _eventHandler?.Invoke(entryCount, startPath);
        }
    }

    private void AddPathsWithUnauthorisedExceptions(string directory)
    {
        // Pre-size list to typical unauthorized path count to avoid reallocations
        PathsWithUnauthorisedExceptions ??= new List<string>(capacity: 100);
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
        TraverseTreePair((_, d) =>
        {
            d.ParentCommonEntry = null;
            return true;
        });
    }

    public void SortAllChildrenByPath()
    {
        Children.Sort((de1, de2) => de1.PathCompareWithDirTo(de2)); // Sort root entries first.
        IsDefaultSort = true;

        TraverseTreePair((_, d) =>
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
    public DateTime Modified
    {
        set => ModifiedTicks = value.Ticks;
        get => DateTime.FromBinary(ModifiedTicks);
    }

    [ProtoMember(12, IsRequired = false)]
    [FlatBufferItem(12)]
    [Key(12)]
    public Flags BitFields { get; set; }

    [ProtoMember(13, IsRequired = false)]
    [FlatBufferItem(13)]
    [MessagePackFormatter(typeof(Infrastructure.Serialization.Hash16Formatter))]
    [Key(13)]
    public Hash16 Hash { get; set; }

    [ProtoMember(14, IsRequired = true)]
    [FlatBufferItem(14)]
    [Key(14)]
    public long ModifiedTicks { get; set; }

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
        Hash = new Hash16(hash);
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
        return sizeCompare == 0
            ? string.Compare(Path, de.Path, StringComparison.OrdinalIgnoreCase)
            : sizeCompare;
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

        return string.Compare(Path, de.Path, StringComparison.OrdinalIgnoreCase);
    }

    // can this be done with TraverseTree ?
    public void SetSummaryFields()
    {
        var size = 0L;
        var dirEntryCount = 0L;
        var fileEntryCount = 0L;

        if (Children != null)
        {
            var childrenDirEntryCount = 0L;
            foreach (var dirEntry in Children)
            {
                if (dirEntry.IsDirectory)
                {
                    dirEntry.SetSummaryFields();
                    ++dirEntryCount;
                }

                size += dirEntry.Size;
                fileEntryCount += dirEntry.FileEntryCount;
                childrenDirEntryCount += dirEntry.DirEntryCount;
            }

            fileEntryCount += Children.Count - dirEntryCount;
            dirEntryCount += childrenDirEntryCount;
        }

        FileEntryCount = (uint)fileEntryCount;
        DirEntryCount = (uint)dirEntryCount;
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
    public IList<DirEntry> Children { get; set; }

    public void AddChild(DirEntry child)
    {
        if (this.Children == null)
            Children = new List<DirEntry>();
        Children.Add(child);
    }

    [ProtoMember(16, IsRequired = true)]
    [FlatBufferItem(16)]
    [Key(16)]
    public long Size { get; set; }

    /// <summary>
    /// RootEntry this is the root path, DirEntry this is the entry name.
    /// </summary>
    [ProtoMember(17, IsRequired = true)]
    [FlatBufferItem(17)]
    [Key(17)]
    public string Path { get; set; }

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
    public bool PathProblem => IsBadPath();

    public void TraverseTreePair(TraverseFunc func)
    {
        TraverseTreePair(new List<ICommonEntry> { this }, func);
    }

    /// <summary>
    /// Iterative tree traversal using a stack. Visits each child entry with its parent.
    /// </summary>
    /// <param name="rootEntries">Entries to traverse</param>
    /// <param name="traverseFunc">Function called for each (parent, child) pair. Returns true to continue, false to stop.</param>
    private static void TraverseTreePair(IEnumerable<ICommonEntry> rootEntries, TraverseFunc traverseFunc)
    {
        if (traverseFunc == null)
        {
            return;
        }

        var stack = InitializeTraversalStack(rootEntries);

        while (stack.Count > 0)
        {
            var parent = stack.Pop();

            if (parent.Children == null)
            {
                continue; // Empty directories may not have Children initialized
            }

            if (!ProcessChildrenWithTraversal(parent, stack, traverseFunc))
            {
                return; // Traversal canceled by func returning false
            }
        }
    }

    /// <summary>
    /// Initializes traversal stack with root entries in reverse order to maintain original ordering.
    /// </summary>
    private static Stack<ICommonEntry> InitializeTraversalStack(IEnumerable<ICommonEntry> rootEntries)
    {
        // Avoid Reverse() intermediate allocation - push in reverse order instead
        var rootArray = rootEntries as ICommonEntry[] ?? rootEntries.ToArray();
        var stack = new Stack<ICommonEntry>(rootArray.Length);

        for (int i = rootArray.Length - 1; i >= 0; i--)
        {
            stack.Push(rootArray[i]);
        }

        return stack;
    }

    /// <summary>
    /// Processes all children of a parent entry, invoking the traversal function and pushing directories to the stack.
    /// </summary>
    /// <returns>True to continue traversal, false if traversal was cancelled</returns>
    private static bool ProcessChildrenWithTraversal(ICommonEntry parent, Stack<ICommonEntry> stack, TraverseFunc traverseFunc)
    {
        foreach (var child in parent.Children)
        {
            if (!traverseFunc(parent, child))
            {
                return false; // Stop traversal
            }

            if (child.IsDirectory)
            {
                stack.Push(child);
            }
        }

        return true;
    }

    /// <summary>
    /// Copies hash values from source tree to destination tree where file metadata matches.
    /// </summary>
    public void TraverseTreesCopyHash(ICommonEntry destination)
    {
        ValidateTreeCopyParameters(this, destination);

        var stack = new Stack<(string, ICommonEntry, ICommonEntry)>(capacity: 64);
        stack.Push((this.Path, this, destination));

        while (stack.Count > 0)
        {
            var (currentPath, sourceEntry, destinationEntry) = stack.Pop();

            if (sourceEntry.Children == null || destinationEntry.Children == null)
            {
                continue; // Skip entries without children
            }

            ProcessChildrenForHashCopy(sourceEntry, destinationEntry, currentPath, stack);
        }
    }

    /// <summary>
    /// Validates that source and destination are compatible for hash copying.
    /// </summary>
    private static void ValidateTreeCopyParameters(ICommonEntry source, ICommonEntry destination)
    {
        if (source == null || destination == null)
        {
            throw new ArgumentException("source and destination must be not null.");
        }

        if (!string.Equals(source.Path, destination.Path, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("source and destination must have same root path.");
        }
    }

    /// <summary>
    /// Processes all children of source and destination, copying hashes and queueing directories.
    /// </summary>
    private static void ProcessChildrenForHashCopy(
        ICommonEntry sourceEntry,
        ICommonEntry destinationEntry,
        string currentPath,
        Stack<(string, ICommonEntry, ICommonEntry)> stack)
    {
        var destinationLookup = BuildDestinationLookup(destinationEntry.Children);

        foreach (var sourceChild in sourceEntry.Children)
        {
            if (!destinationLookup.TryGetValue(sourceChild.Path, out var destinationChild))
            {
                continue; // Source entry not found in destination
            }

            if (AreBothDirectories(sourceChild, destinationChild))
            {
                var fullPath = System.IO.Path.Combine(currentPath, sourceChild.Path);
                stack.Push((fullPath, sourceChild, destinationChild));
            }
            else if (AreBothFilesWithMatchingMetadata(sourceChild, destinationChild))
            {
                TryCopyHashIfBeneficial(sourceChild, destinationChild);
            }
        }
    }

    /// <summary>
    /// Builds a dictionary for O(1) lookups of destination children by path.
    /// </summary>
    private static Dictionary<string, DirEntry> BuildDestinationLookup(IList<DirEntry> children)
    {
        var lookup = new Dictionary<string, DirEntry>(children.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var child in children)
        {
            // TryAdd handles potential duplicate paths gracefully (keeps first, ignores rest)
            lookup.TryAdd(child.Path, child);
        }

        return lookup;
    }

    /// <summary>
    /// Checks if both entries are directories.
    /// </summary>
    private static bool AreBothDirectories(ICommonEntry source, ICommonEntry destination)
    {
        return source.IsDirectory && destination.IsDirectory;
    }

    /// <summary>
    /// Checks if both entries are files with matching metadata (size and modified time).
    /// </summary>
    private static bool AreBothFilesWithMatchingMetadata(ICommonEntry source, ICommonEntry destination)
    {
        return !source.IsDirectory
            && source.Modified == destination.Modified
            && source.Size == destination.Size;
    }

    /// <summary>
    /// Copies hash from source to destination if it provides value (new hash or upgrading partial to full).
    /// </summary>
    private static void TryCopyHashIfBeneficial(ICommonEntry source, ICommonEntry destination)
    {
        if (!source.IsHashDone)
        {
            return; // Source has no hash to copy
        }

        var shouldCopy = !destination.IsHashDone  // Destination has no hash
            || (source.IsPartialHash == false && destination.IsPartialHash);  // Upgrading partial to full

        if (shouldCopy)
        {
            destination.IsPartialHash = source.IsPartialHash;
            destination.Hash = source.Hash;
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
        for (ICommonEntry entry = this; entry != null; entry = entry.ParentCommonEntry)
        {
            activatedDirEntryList.Add(entry);
        }

        activatedDirEntryList.Reverse(); // list now from root to this.
        return activatedDirEntryList;
    }

    public bool ExistsOnFileSystem() => Directory.Exists(FullPath);

    /// <summary>
    /// Is a bad path
    /// </summary>
    /// <returns>False if Null or Empty, True if entry name ends with Space or Period which is a problem on windows file systems.</returns>
    public bool IsBadPath()
    {
        // This probably needs to check all parent paths if this is a root entry.
        // Not high priority as will not generally be able to specify a folder with a problem path at or above root.
        return !string.IsNullOrEmpty(Path) && (Path.EndsWith(' ') || Path.EndsWith('.'));
    }
}