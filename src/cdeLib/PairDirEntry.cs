using System.Diagnostics;
using System.IO;
using cdeLib.Entities;
using JetBrains.Annotations;

namespace cdeLib;

[DebuggerDisplay("Size = {ChildDE.Size}")]
public class PairDirEntry
{
    public readonly ICommonEntry ParentDE;

    public readonly ICommonEntry ChildDE;

    /// <summary>
    /// RootEntry for this pair dir, if not set looked up and cached in GetRootEntry().
    /// </summary>
    [CanBeNull]
    private RootEntry _rootEntry;

    /// <summary>
    /// true if path or parent path ends with bad characters for NTFS, like Space or Period
    /// </summary>
    public readonly bool PathProblem;

    public string FullPath => EntryHelper.MakeFullPath(ParentDE, ChildDE);

    public PairDirEntry(ICommonEntry parent, ICommonEntry child)
    {
        ParentDE = parent;
        ChildDE = child;
        PathProblem = ParentDE.PathProblem || ChildDE.PathProblem;
    }

    /// <summary>
    /// TODO add checks for root and volume name for now just use path ?
    /// </summary>
    /// <returns></returns>
    public bool ExistsOnFileSystem()
    {
        var path = FullPath;
        return ChildDE.IsDirectory
            ? Directory.Exists(path)
            : File.Exists(path);
    }

    /// <summary>
    /// Get the RootEntry of a PairDirEntry.
    /// This is lazy and cached so we don't store RootEntry on DirEntry only here.
    /// This could be done eagerly in constructor, but for non displayed results
    /// its probably not worth doing eagerly.
    /// </summary>
    /// <returns>The RootEntry for any pair dire entry.</returns>
    public RootEntry GetRootEntry()
    {
        // ReSharper disable once InvertIf
        if (_rootEntry == null)
        {
            var rootEntryCursor = ParentDE;
            while (rootEntryCursor.ParentCommonEntry != null)
            {
                rootEntryCursor = rootEntryCursor.ParentCommonEntry;
            }
            _rootEntry = rootEntryCursor as RootEntry; // every DirEntry has a RootEntry
        }

        return _rootEntry;
    }
}