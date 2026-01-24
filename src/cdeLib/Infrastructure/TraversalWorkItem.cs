using cdeLib.Entities;

namespace cdeLib.Infrastructure;

/// <summary>
/// Work item representing a subtree to be processed
/// </summary>
public class TraversalWorkItem
{
    public ICommonEntry Parent { get; set; }
    public ICommonEntry Entry { get; set; }
    public int Depth { get; set; }
    public int EstimatedSize { get; set; }

    public TraversalWorkItem(ICommonEntry parent, ICommonEntry entry, int depth = 0)
    {
        Parent = parent;
        Entry = entry;
        Depth = depth;
        EstimatedSize = EstimateSize(entry);
    }

    private static int EstimateSize(ICommonEntry entry)
    {
        // Simple heuristic: count immediate children + estimate for subdirectories
        if (!entry.IsDirectory || entry.Children == null)
            return 1;

        var childCount = entry.Children.Count;
        // Assume ~20% of children are directories (avoids LINQ iteration)
        var estimatedDirs = childCount / 5;
        return 1 + childCount + estimatedDirs * 10;
    }
}