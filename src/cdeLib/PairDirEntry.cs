using System.Diagnostics;
using System.IO;
using cdeLib.Entities;

namespace cdeLib;

[DebuggerDisplay("Size = {ChildDE.Size}")]
public class PairDirEntry
{
    public readonly ICommonEntry ParentDE;

    public readonly ICommonEntry ChildDE;

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
}