using System.Collections.Generic;

namespace cdeAppCore.Dtos;

/// <summary>
/// One node of a treemap: a flat row linked to its parent by <see cref="ParentId"/>, the shape a
/// hierarchy layout (e.g. TanStack Charts' treemap mark in parent mode) consumes directly.
/// </summary>
public sealed class TreemapRowDto
{
    /// <summary>Stable node identity: the entry ref ("catalog-index"), or "parentId/+" for a merged row.</summary>
    public string NodeId { get; set; }

    /// <summary>Parent node identity; null only for the mapped root row.</summary>
    public string ParentId { get; set; }

    public string Name { get; set; }

    /// <summary>The entry's real size in bytes (a directory's aggregated subtree size).</summary>
    public long Size { get; set; }

    /// <summary>
    /// Tile area contribution. Layouts sum values up the hierarchy, so a directory expanded into
    /// child rows contributes 0 and its area comes from its children; leaves contribute their size.
    /// </summary>
    public long Value { get; set; }

    public bool IsDirectory { get; set; }

    /// <summary>Number of entries folded into this row when it is a "(N smaller …)" row, else 0.</summary>
    public int MergedCount { get; set; }

    /// <summary>The catalog entry this row represents; null for a merged row.</summary>
    public EntryRefDto? Ref { get; set; }
}

/// <summary>A depth-limited treemap of one directory's subtree.</summary>
public sealed class TreemapResponse
{
    public EntryRefDto Root { get; set; }
    public string RootFullPath { get; set; }
    public long RootSize { get; set; }

    /// <summary>Levels below the root actually requested (after clamping).</summary>
    public int Depth { get; set; }

    /// <summary>True when the row cap forced extra merging or left directories unexpanded.</summary>
    public bool Truncated { get; set; }

    public List<TreemapRowDto> Rows { get; set; } = [];
}
