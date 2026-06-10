using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeMemProbe;

/// <summary>
/// Deterministic synthetic <c>.cde</c> catalog generator used to measure memory footprint and
/// search performance reproducibly across optimization phases. Builds a <see cref="RootEntry"/>
/// tree in memory with a realistic file:directory ratio, then the caller saves/loads it.
///
/// The SAME fixture (same seed + parameters) must be used for every phase so per-entry-byte and
/// search-timing deltas are attributable to the change under test, not to fixture drift.
/// </summary>
public static class SyntheticCatalog
{
    // Representative extension/prefix spread; interning means duplicates are deduplicated in memory
    // exactly as a real catalog would be.
    private static readonly string[] Extensions =
        [".txt", ".jpg", ".pdf", ".doc", ".mp4", ".zip", ".exe", ".dll", ".cs", ".json", ".png", ".log"];

    private static readonly string[] Prefixes =
        ["Document", "Image", "Video", "Archive", "Config", "Data", "Log", "Report", "Backup", "Cache"];

    private static readonly DateTime BaseDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Build a synthetic tree of approximately <paramref name="targetEntryCount"/> entries
    /// (files + directories combined), using a ~<paramref name="filesPerDir"/>:1 file/dir ratio.
    /// Generation is breadth-first so the tree is bushy and shallow like a real file system.
    /// </summary>
    /// <param name="targetEntryCount">Approximate total of files + directories to generate.</param>
    /// <param name="withHashes">When true, every file gets an MD5-sized hash set (worst case for memory).</param>
    /// <param name="seed">Deterministic RNG seed; keep fixed across phases.</param>
    /// <param name="filesPerDir">Files added per directory (drives the file:dir ratio).</param>
    /// <param name="subDirsPerDir">Sub-directories added per directory (drives breadth).</param>
    public static RootEntry Generate(
        int targetEntryCount,
        bool withHashes = false,
        int seed = 42,
        int filesPerDir = 50,
        int subDirsPerDir = 5,
        bool sharedNames = false)
    {
        var random = new Random(seed);
        var root = new RootEntry
        {
            Path = @"C:\synthetic",
            DefaultFileName = "synthetic.cde",
            DriveLetterHint = "C",
            VolumeName = "SYNTHETIC",
            Description = $"Synthetic catalog ~{targetEntryCount} entries (hashes={withHashes})",
            Children = new List<DirEntry>(),
        };

        var created = 0;
        // Directories that can still receive children. Breadth-first never drains before the target
        // is hit because each directory enqueues more directories than it dequeues.
        var queue = new Queue<DirEntry>();

        Populate(root, null);
        while (queue.Count > 0 && created < targetEntryCount)
        {
            Populate(null, queue.Dequeue());
        }

        root.SetInMemoryFields();
        return root;

        void Populate(RootEntry? rootParent, DirEntry? dirParent)
        {
            for (var i = 0; i < filesPerDir && created < targetEntryCount; i++)
            {
                var file = new DirEntry(false);
                // sharedNames: every file shares one interned name, so the measured footprint
                // excludes per-file name strings — the delta vs normal isolates the name cost.
                if (sharedNames)
                {
                    file.SetPath("x");
                }
                else
                {
                    var ext = Extensions[random.Next(Extensions.Length)];
                    var prefix = Prefixes[random.Next(Prefixes.Length)];
                    file.SetPath($"{prefix}_{created:D7}{ext}");
                }
                file.Size = random.Next(1, 50_000_000);
                file.Modified = BaseDate.AddMinutes(random.Next(0, 5_000_000));
                if (withHashes)
                {
                    // Mix of unique and duplicate hashes (every 17th repeats) to exercise dupe paths.
                    file.SetHash(created % 17 == 0 ? 17 : created);
                }

                if (rootParent != null) rootParent.AddChild(file);
                else dirParent!.AddChild(file);
                created++;
            }

            for (var i = 0; i < subDirsPerDir && created < targetEntryCount; i++)
            {
                var dir = new DirEntry(true);
                dir.SetPath($"dir_{created:D7}");
                dir.Modified = BaseDate.AddMinutes(random.Next(0, 5_000_000));
                if (rootParent != null) rootParent.AddChild(dir);
                else dirParent!.AddChild(dir);
                created++;
                queue.Enqueue(dir);
            }
        }
    }
}
