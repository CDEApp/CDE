using System;
using cdeLib.Entities;
using cdeLib.Entities.Soa;

namespace cdeLib;

/// <summary>How many files and folders an entry has directly below it, and in all its subfolders. Zero for a file.</summary>
public readonly record struct EntryCounts(int ChildFiles, int ChildFolders, int DescendantFiles, int DescendantFolders)
{
    public int Children => ChildFiles + ChildFolders;

    public int Descendants => DescendantFiles + DescendantFolders;
}

/// <summary>
/// The <see cref="EntryCounts"/> of every entry of one catalog, built once per search by a pass over the parent links
/// (the same for the mmap reader and the in-memory store, both through <see cref="IEntrySource"/>). Built only when the
/// query has a child-count: or descendant-count: function, so other searches never pay for it.
/// </summary>
public sealed class EntryCountTable
{
    private readonly int[] _childFiles;
    private readonly int[] _childFolders;
    private readonly int[] _descendantFiles; // null when not asked for
    private readonly int[] _descendantFolders;

    private EntryCountTable(int[] childFiles, int[] childFolders, int[] descendantFiles, int[] descendantFolders)
    {
        _childFiles = childFiles;
        _childFolders = childFolders;
        _descendantFiles = descendantFiles;
        _descendantFolders = descendantFolders;
    }

    public EntryCounts Of(int i) => new(
        _childFiles[i],
        _childFolders[i],
        _descendantFiles?[i] ?? 0,
        _descendantFolders?[i] ?? 0);

    public static EntryCountTable Build(IEntrySource source, bool descendants)
    {
        ArgumentNullException.ThrowIfNull(source);
        var n = source.Count;
        var parents = new int[n];
        var folders = new bool[n];
        for (var i = 1; i < n; i++)
        {
            parents[i] = source.ParentOf(i);
            folders[i] = source.IsDirectory(i);
        }

        return FromParents(parents, folders, descendants);
    }

    /// <summary>
    /// Counts from the parent link and the folder flag of every entry (index 0 is the root and has no parent).
    /// A parent index outside the catalog, such as <see cref="EntryStore.None"/>, means no parent.
    /// </summary>
    public static EntryCountTable FromParents(int[] parents, bool[] folders, bool descendants)
    {
        var n = parents.Length;
        var childFiles = new int[n];
        var childFolders = new int[n];
        var parentsFirst = true; // every parent has a lower index than its children, as the writers lay them out

        for (var i = 1; i < n; i++)
        {
            var p = parents[i];
            if (p < 0 || p >= n) continue;
            if (folders[i]) childFolders[p]++;
            else childFiles[p]++;
            if (p >= i) parentsFirst = false;
        }

        if (!descendants) return new EntryCountTable(childFiles, childFolders, null, null);

        var descendantFiles = new int[n];
        var descendantFolders = new int[n];
        if (parentsFirst)
        {
            // Children come after their parent, so by the time entry i is added to its parent, i holds its own total.
            for (var i = n - 1; i >= 1; i--)
            {
                var p = parents[i];
                if (p < 0) continue;
                descendantFiles[p] += (folders[i] ? 0 : 1) + descendantFiles[i];
                descendantFolders[p] += (folders[i] ? 1 : 0) + descendantFolders[i];
            }
        }
        else
        {
            // A catalog whose parents are not all before their children: count by walking up from each entry. The
            // step limit stops a corrupt cycle.
            for (var i = 1; i < n; i++)
            {
                var steps = 0;
                for (var p = parents[i]; p >= 0 && p < n && steps < n; p = parents[p], steps++)
                {
                    if (folders[i]) descendantFolders[p]++;
                    else descendantFiles[p]++;
                }
            }
        }

        return new EntryCountTable(childFiles, childFolders, descendantFiles, descendantFolders);
    }
}
