using System;
using System.Collections.Generic;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Treemap;

/// <summary>Builds depth-limited treemap rows for a directory's subtree.</summary>
public interface ITreemapService
{
    /// <param name="root">The directory to map.</param>
    /// <param name="depth">Levels below <paramref name="root"/> to include, clamped to 1–5.</param>
    /// <param name="minFraction">
    /// Entries smaller than this fraction of the root's size are folded, per folder, into one
    /// "(N smaller …)" row. The caller derives it from its viewport (≈ the smallest legible tile).
    /// </param>
    /// <param name="maxRows">Hard cap on rows; past it the smallest entries are merged too.</param>
    TreemapResponse Build(EntryRefDto root, int depth, double minFraction,
        int maxRows = TreemapService.DefaultMaxRows);
}

/// <summary>
/// Walks the catalog breadth-first from the mapped directory, so when the row cap bites it is the
/// deepest, smallest detail that is lost. Directory sizes are the stored aggregated subtree sizes,
/// so no subtree is traversed beyond the requested depth.
/// </summary>
public sealed class TreemapService : ITreemapService
{
    public const int DefaultMaxRows = 5000;
    public const int MinDepth = 1;
    public const int MaxDepth = 5;

    private readonly ICatalogSession _session;

    public TreemapService(ICatalogSession session)
    {
        _session = session;
    }

    public TreemapResponse Build(EntryRefDto root, int depth, double minFraction, int maxRows = DefaultMaxRows)
    {
        depth = Math.Clamp(depth, MinDepth, MaxDepth);
        maxRows = Math.Max(2, maxRows);
        if (double.IsNaN(minFraction)) minFraction = 0;
        minFraction = Math.Clamp(minFraction, 0, 0.5);

        using var lease = _session.AcquireRead(root.CatalogId); // a rescan swap waits for this build
        var source = lease.Source;
        var rootIndex = root.EntryIndex;
        var rootSize = source.SizeOf(rootIndex);
        if (rootIndex == 0 && rootSize == 0) rootSize = source.RootSize;
        var minBytes = minFraction > 0 ? (long)Math.Ceiling(rootSize * minFraction) : 0;

        var response = new TreemapResponse
        {
            Root = root,
            RootFullPath = source.FullPath(rootIndex),
            RootSize = rootSize,
            Depth = depth,
        };
        var rows = response.Rows;

        var rootRow = EntryRow(source, root.CatalogId, rootIndex, parentId: null, rootSize);
        rootRow.Name = rootIndex == 0 ? response.RootFullPath : rootRow.Name; // catalog root reads as "C:\"
        rootRow.IsDirectory = true;
        rows.Add(rootRow);

        // Rows queued for expansion are emitted with Value 0; one that is never expanded (row cap)
        // gets its size back so the layout loses no area.
        var queue = new Queue<(TreemapRowDto Row, int Index, int Level)>();
        queue.Enqueue((rootRow, rootIndex, 0));

        var children = new List<(int Index, long Size)>();
        var keep = new List<(int Index, long Size)>();
        while (queue.Count > 0)
        {
            var (parentRow, parentIndex, level) = queue.Dequeue();
            var available = maxRows - rows.Count;
            if (available <= 0)
            {
                parentRow.Value = parentRow.Size;
                response.Truncated = true;
                continue;
            }

            children.Clear();
            foreach (var c in source.ChildrenOf(parentIndex))
            {
                var size = source.SizeOf(c);
                if (size > 0) children.Add((c, size)); // zero-size entries have no area to draw
            }

            if (children.Count == 0)
            {
                parentRow.Value = parentRow.Size;
                continue;
            }

            // Largest first: the row cap and the merge both fold from the small end.
            children.Sort((a, b) => b.Size.CompareTo(a.Size));

            keep.Clear();
            var mergedCount = 0;
            var mergedSize = 0L;
            var mergedAllFiles = true;
            void Fold((int Index, long Size) entry)
            {
                mergedCount++;
                mergedSize += entry.Size;
                if (source.IsDirectory(entry.Index)) mergedAllFiles = false;
            }

            var smallCount = 0;
            foreach (var child in children)
                if (child.Size < minBytes) smallCount++;

            // A lone small entry stays itself: "(1 smaller file)" would just hide its name.
            var mergeSmall = smallCount > 1;
            foreach (var child in children)
            {
                if (mergeSmall && child.Size < minBytes) Fold(child);
                else keep.Add(child);
            }

            // Row cap: keep the largest that fit, reserving one row for the merged remainder.
            var needed = keep.Count + (mergedCount > 0 ? 1 : 0);
            if (needed > available)
            {
                response.Truncated = true;
                var fit = Math.Max(0, available - 1);
                for (var i = keep.Count - 1; i >= fit; i--) Fold(keep[i]);
                keep.RemoveRange(fit, keep.Count - fit);
            }

            foreach (var (index, size) in keep)
            {
                var row = EntryRow(source, root.CatalogId, index, parentRow.NodeId, size);
                rows.Add(row);
                var expandable = row.IsDirectory && level + 1 < depth && source.FirstChildOf(index) != EntryStore.None;
                if (expandable)
                {
                    row.Value = 0;
                    queue.Enqueue((row, index, level + 1));
                }
            }

            if (mergedCount > 0)
            {
                rows.Add(new TreemapRowDto
                {
                    NodeId = parentRow.NodeId + "/+",
                    ParentId = parentRow.NodeId,
                    Name = $"({mergedCount} smaller {(mergedAllFiles ? "files" : "items")})",
                    Size = mergedSize,
                    Value = mergedSize,
                    MergedCount = mergedCount,
                });
            }

            parentRow.Value = 0;
        }

        return response;
    }

    private static TreemapRowDto EntryRow(IEntrySource source, int catalogId, int index, string parentId, long size)
    {
        var entryRef = new EntryRefDto(catalogId, index);
        return new TreemapRowDto
        {
            NodeId = $"{catalogId}-{index}",
            ParentId = parentId,
            Name = source.FullName(index),
            Size = size,
            Value = size,
            IsDirectory = source.IsDirectory(index),
            Ref = entryRef,
        };
    }
}
