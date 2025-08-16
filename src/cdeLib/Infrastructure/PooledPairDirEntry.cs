using System.Diagnostics;
using System.IO;
using cdeLib.Entities;
using JetBrains.Annotations;

namespace cdeLib.Infrastructure;

/// <summary>
/// Pooled version of PairDirEntry that can be reset and reused to reduce allocations
/// </summary>
[DebuggerDisplay("Size = {ChildDE?.Size}")]
public class PooledPairDirEntry : IPairDirEntry
{
    public ICommonEntry ParentDE { get; private set; }
    public ICommonEntry ChildDE { get; private set; }
    
    [CanBeNull]
    private RootEntry _rootEntry;
    
    public bool PathProblem { get; private set; }
    
    public string FullPath => EntryHelper.MakeFullPath(ParentDE, ChildDE);

    /// <summary>
    /// Initialize this pooled entry with new parent/child data
    /// </summary>
    public void Initialize(ICommonEntry parent, ICommonEntry child)
    {
        ParentDE = parent;
        ChildDE = child;
        PathProblem = parent?.PathProblem == true || child?.PathProblem == true;
        _rootEntry = null; // Reset cached root entry
    }

    /// <summary>
    /// Reset this entry for return to pool
    /// </summary>
    public void Reset()
    {
        ParentDE = null;
        ChildDE = null;
        PathProblem = false;
        _rootEntry = null;
    }

    public bool ExistsOnFileSystem()
    {
        var path = FullPath;
        return ChildDE.IsDirectory
            ? Directory.Exists(path)
            : File.Exists(path);
    }

    public RootEntry GetRootEntry()
    {
        if (_rootEntry == null)
        {
            var rootEntryCursor = ParentDE;
            while (rootEntryCursor.ParentCommonEntry != null)
            {
                rootEntryCursor = rootEntryCursor.ParentCommonEntry;
            }
            _rootEntry = rootEntryCursor as RootEntry;
        }

        return _rootEntry;
    }
}

/// <summary>
/// Static pool manager for PairDirEntry objects
/// </summary>
public static class PairDirEntryPool
{
    private static readonly ObjectPool<PooledPairDirEntry> Pool = 
        new(() => new PooledPairDirEntry(), entry => entry.Reset(), 200);

    public static PooledPairDirEntry Get(ICommonEntry parent, ICommonEntry child)
    {
        var entry = Pool.Get();
        entry.Initialize(parent, child);
        return entry;
    }

    public static void Return(PooledPairDirEntry entry)
    {
        Pool.Return(entry);
    }

    public static void Clear()
    {
        Pool.Clear();
    }

    /// <summary>
    /// Create a regular PairDirEntry from pooled data when persistence is needed
    /// </summary>
    public static PairDirEntry CreatePersistent(ICommonEntry parent, ICommonEntry child)
    {
        return new PairDirEntry(parent, child);
    }
}