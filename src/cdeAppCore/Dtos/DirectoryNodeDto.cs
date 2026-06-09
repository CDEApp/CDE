using System;

namespace cdeAppCore.Dtos;

/// <summary>
/// A serializable directory-tree node / directory-listing row. Replaces the WinForms
/// <c>TreeNode</c>/<c>ListViewItem</c> at the boundary: it carries the entry's name, full path, the
/// raw size/date for listing rows, and <see cref="HasChildren"/> so a frontend can render a lazily-
/// expandable tree without paging in the whole subtree. <see cref="Ref"/> addresses the entry for
/// follow-up calls (children, path, shell actions).
/// </summary>
public sealed class DirectoryNodeDto
{
    public EntryRefDto Ref { get; set; }

    /// <summary>The node label / row name (the entry's <c>Path</c> component).</summary>
    public string Name { get; set; }

    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }

    /// <summary>True when this directory has at least one child directory (drives tree expandability).</summary>
    public bool HasChildren { get; set; }

    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public bool IsModifiedBad { get; set; }
    public bool IsReparsePoint { get; set; }
}
