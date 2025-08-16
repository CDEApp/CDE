using cdeLib.Entities;

namespace cdeLib.Entities;

/// <summary>
/// Interface for pair directory entries that provides access to parent/child entry relationships
/// and common operations for both regular and pooled implementations
/// </summary>
public interface IPairDirEntry
{
    /// <summary>
    /// The parent entry in the directory hierarchy
    /// </summary>
    ICommonEntry ParentDE { get; }

    /// <summary>
    /// The child entry (file or directory)
    /// </summary>
    ICommonEntry ChildDE { get; }

    /// <summary>
    /// True if path or parent path ends with bad characters for NTFS, like Space or Period
    /// </summary>
    bool PathProblem { get; }

    /// <summary>
    /// The full path combining parent and child paths
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// Check if this entry exists on the file system
    /// </summary>
    /// <returns>True if the file or directory exists</returns>
    bool ExistsOnFileSystem();

    /// <summary>
    /// Get the root entry for this pair directory entry
    /// </summary>
    /// <returns>The root entry</returns>
    RootEntry GetRootEntry();
}