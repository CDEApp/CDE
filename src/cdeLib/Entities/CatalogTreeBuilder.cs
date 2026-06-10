using System.Collections.Generic;
using cdeLib.Entities.Columnar;

namespace cdeLib.Entities;

/// <summary>
/// Reconstructs a mutable <see cref="RootEntry"/> tree from a read-only <see cref="IEntrySource"/>
/// (an <see cref="Soa.EntryStore"/> or a memory-mapped <see cref="ColumnarCatalogReader"/>). This is
/// the inverse of <see cref="Soa.EntryStore.Build"/>, used by the batch <c>hash</c>/<c>dupes</c>
/// commands: they need the existing tree-based hashing engine (two-phase partial→full hashing,
/// cross-catalog size pairing, parallel-by-volume), so they rebuild a tree from the columnar catalog,
/// mutate it (hashes), and write a fresh <c>.cdex</c> back. Field values (modified ticks, flags, hash)
/// are copied verbatim so the rewritten catalog round-trips exactly.
/// </summary>
public static class CatalogTreeBuilder
{
    public static RootEntry FromSource(IEntrySource s)
    {
        var root = new RootEntry
        {
            Path = s.RootPath,
            VolumeName = s.VolumeName,
            DefaultFileName = s.DefaultFileName,
            ActualFileName = s.ActualFileName,
            DriveLetterHint = s.DriveLetterHint,
            Description = s.Description,
            AvailSpace = s.AvailSpace,
            TotalSpace = s.TotalSpace,
            ScanStartUtcTicks = s.ScanStartUtcTicks,
            ScanEndUtcTicks = s.ScanEndUtcTicks,
            ModifiedTicks = s.ModifiedTicksOf(0),
            BitFields = s.FlagsOf(0),
        };

        var created = new ICommonEntry[s.Count];
        created[0] = root;

        // Walk the index tree (root = 0) creating a DirEntry per entry, preserving sibling order.
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            foreach (var c in s.ChildrenOf(p))
            {
                var d = new DirEntry(s.IsDirectory(c))
                {
                    Path = s.FullName(c),
                    Size = s.SizeOf(c),
                    ModifiedTicks = s.ModifiedTicksOf(c),
                    BitFields = s.FlagsOf(c),
                };
                if (s.HasHash && s.IsHashDone(c)) d.Hash = s.HashOf(c);

                created[c] = d;
                created[p].AddChild(d);
                if (s.IsDirectory(c)) stack.Push(c);
            }
        }

        root.SetInMemoryFields();
        return root;
    }

    /// <summary>
    /// Open each columnar <c>.cdex</c> file, reconstruct its tree, and tag it with the source path
    /// (<see cref="RootEntry.ActualFileName"/>) so a mutated catalog can be written straight back.
    /// Each mapping is closed before returning — the tree is a full in-memory copy.
    /// </summary>
    public static List<RootEntry> FromColumnarFiles(IEnumerable<string> cdexFiles)
    {
        var trees = new List<RootEntry>();
        foreach (var file in cdexFiles)
        {
            using var reader = new ColumnarCatalogReader(file);
            var tree = FromSource(reader);
            tree.ActualFileName = file;
            trees.Add(tree);
        }
        return trees;
    }
}
