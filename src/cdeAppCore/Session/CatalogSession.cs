using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using Serilog;

namespace cdeAppCore.Session;

/// <summary>
/// Default <see cref="ICatalogSession"/>. Holds one <see cref="CatalogSlot"/> per loaded catalog — a
/// zero-copy <see cref="ColumnarCatalogReader"/> over a memory-mapped <c>.cdex</c> when present,
/// otherwise an in-memory <see cref="EntryStore"/> built from a loaded <c>.cde</c> tree. Catalog id is
/// the index into that list; an entry is addressed by (catalog id, entry index). Each slot has a
/// reader/writer lock so a rescan can swap one catalog's source without disturbing the others.
/// </summary>
public sealed partial class CatalogSession : ICatalogSession
{
    private readonly ILoadCatalogService _loadCatalogService;
    private readonly ILogger _logger;

    private List<CatalogSlot> _slots = [];
    private List<ICommonEntry> _roots = [];

    // Serialises LoadAsync against rescans: a load runs only with no rescan running, and no rescan can
    // start while a load runs. Guards _loading and the slots' rescan flags.
    private readonly object _gate = new();
    private bool _loading;
    private readonly Func<string, ColumnarCatalogReader> _openReader;

    /// <param name="openReader">Opens a .cdex; defaults to mapping it. A seam for failure-injection tests.</param>
    public CatalogSession(ILoadCatalogService loadCatalogService, ILogger logger,
        Func<string, ColumnarCatalogReader> openReader = null)
    {
        _loadCatalogService = loadCatalogService;
        _logger = logger;
        _openReader = openReader ?? (path => new ColumnarCatalogReader(path));
    }

    public int CatalogCount => _slots.Count;

    // An unavailable slot (failed swap that could not be rolled back) counts as empty rather than throwing.
    public long TotalEntryCount => _slots.Sum(slot =>
    {
        if (!slot.TryEnterRead(out var lease)) return 0L;
        using (lease) return (long)lease.Source.RootFileEntryCount + lease.Source.RootDirEntryCount;
    });

    public IReadOnlyList<ICommonEntry> Roots => _roots;

    public IEntrySource GetSource(int catalogId) => _slots[catalogId].Source
        ?? throw new InvalidOperationException("The catalog is unavailable.");

    public CatalogReadLease AcquireRead(int catalogId) => _slots[catalogId].EnterRead();

    public async Task LoadAsync(string configPath, IProgress<CatalogLoadProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_loading) throw new InvalidOperationException("A catalog load is already running.");
            if (_slots.Any(s => s.IsRescanning))
                throw new InvalidOperationException("Cannot reload while a catalog is being rescanned.");
            if (_creating > 0)
                throw new InvalidOperationException("Cannot reload while a catalog is being created.");
            _loading = true;
            RememberCatalogDir(configPath);
        }

        try
        {
            await LoadCoreAsync(configPath, progress, cancellationToken);
        }
        finally
        {
            lock (_gate) _loading = false;
        }
    }

    private async Task LoadCoreAsync(string configPath, IProgress<CatalogLoadProgress> progress,
        CancellationToken cancellationToken)
    {
        DisposeSlots();

        // Dedup by full path: the loader scans both "." and configPath, which collapse to the same
        // directory (and thus duplicate catalogs) when the host runs in its catalog directory.
        var cdex = _loadCatalogService.GetColumnarFiles(configPath)
            ?.Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cdex is { Count: > 0 })
        {
            _slots = ReadColumnar(cdex);
        }
        else
        {
            var trees = await _loadCatalogService.LoadRootEntriesAsync(
                configPath,
                (current, total, message) => progress?.Report(new CatalogLoadProgress(current, total, message)),
                cancellationToken);
            _slots = BuildStores(trees);
        }

        _roots = _slots.Select(ICommonEntry (s) => new EntryRef(s.Source, 0)).ToList();
    }

    // Open each .cdex as a zero-copy mmap reader. Unreadable files are skipped (logged).
    private List<CatalogSlot> ReadColumnar(IList<string> cdexFiles)
    {
        var slots = new List<CatalogSlot>(cdexFiles.Count);
        foreach (var file in cdexFiles)
        {
            try
            {
                slots.Add(new CatalogSlot(_openReader(file), file));
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Skipping unreadable .cdex {File}", file);
            }
        }

        return slots;
    }

    private static List<CatalogSlot> BuildStores(List<RootEntry> trees)
    {
        var slots = new List<CatalogSlot>(trees?.Count ?? 0);
        if (trees == null) return slots;
        for (var i = 0; i < trees.Count; i++)
        {
            var path = string.IsNullOrEmpty(trees[i].ActualFileName) ? null : Path.GetFullPath(trees[i].ActualFileName);
            slots.Add(new CatalogSlot(EntryStore.Build(trees[i]), path));
            trees[i] = null; // release the tree so it can be collected
        }

        return slots;
    }

    public IReadOnlyList<CatalogInfoDto> GetCatalogs()
    {
        var list = new List<CatalogInfoDto>(_slots.Count);
        for (var i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].TryEnterRead(out var lease)) continue; // unavailable slot: skip, never throw
            using var _ = lease;
            var s = lease.Source;
            list.Add(new CatalogInfoDto
            {
                CatalogId = i,
                RootPath = s.RootPath,
                VolumeName = s.VolumeName,
                DirEntryCount = s.RootDirEntryCount,
                FileEntryCount = s.RootFileEntryCount,
                DriveLetterHint = s.DriveLetterHint,
                RootSize = s.RootSize,
                AvailSpace = s.AvailSpace,
                TotalSpace = s.TotalSpace,
                ScanStartUtcTicks = s.ScanStartUtcTicks,
                ScanEndUtcTicks = s.ScanEndUtcTicks,
                ActualFileName = s.ActualFileName,
                DefaultFileName = s.DefaultFileName,
                Description = s.Description,
                SourcePath = _slots[i].SourcePath,
                FollowJunctions = s.FollowJunctions
            });
        }

        return list;
    }

    public IReadOnlyList<DirectoryNodeDto> GetChildren(EntryRefDto parent, bool foldersOnly = false,
        int skip = 0, int take = int.MaxValue, bool withHasChildren = true)
    {
        using var lease = AcquireRead(parent.CatalogId);
        var source = lease.Source;
        var result = new List<DirectoryNodeDto>();
        if (take != int.MaxValue && take <= 0) return result; // matches Enumerable.Take semantics

        // Walk the sibling chain by index, applying filter/skip/take before anything is built, so only
        // the returned page allocates (whatever the folder size).
        var first = source.FirstChildOf(parent.EntryIndex);
        if (first == EntryStore.None) return result;

        if (take != int.MaxValue) result.Capacity = Math.Min(take, 256);
        var parentPath = source.FullPath(parent.EntryIndex); // once; each child adds separator + name
        var skipped = 0;
        for (var c = first; c != EntryStore.None && result.Count < take; c = source.NextSiblingOf(c))
        {
            if (foldersOnly && !source.IsDirectory(c)) continue;
            if (skipped < skip) { skipped++; continue; }

            var name = source.FullName(c);
            result.Add(ToNodeDto(parent.CatalogId, source, c, name, PathJoin.Child(parentPath, name),
                withHasChildren));
        }

        return result;
    }

    public IReadOnlyList<DirectoryNodeDto> GetPath(EntryRefDto entry)
    {
        using var lease = AcquireRead(entry.CatalogId);
        var source = lease.Source;
        var chain = new List<int>(8);
        for (var cur = entry.EntryIndex; cur != EntryStore.None; cur = source.ParentOf(cur)) chain.Add(cur);
        chain.Reverse();

        // Root-first; each full path is the previous one plus separator and name.
        var result = new List<DirectoryNodeDto>(chain.Count);
        string path = null;
        foreach (var idx in chain)
        {
            var name = source.FullName(idx);
            path = path == null ? source.FullPath(idx) : PathJoin.Child(path, name);
            result.Add(ToNodeDto(entry.CatalogId, source, idx, name, path));
        }

        return result;
    }

    public string ResolveFullPath(EntryRefDto entry)
    {
        using var lease = AcquireRead(entry.CatalogId);
        return lease.Source.FullPath(entry.EntryIndex);
    }

    public bool ExistsOnFileSystem(EntryRefDto entry)
    {
        string path;
        bool isDirectory;
        using (var lease = AcquireRead(entry.CatalogId))
        {
            path = lease.Source.FullPath(entry.EntryIndex);
            isDirectory = lease.Source.IsDirectory(entry.EntryIndex);
        }

        return isDirectory ? Directory.Exists(path) : File.Exists(path);
    }

    private static DirectoryNodeDto ToNodeDto(int catalogId, IEntrySource source, int index,
        string name, string fullPath, bool withHasChildren = true)
    {
        var isDirectory = source.IsDirectory(index);
        var flags = source.FlagsOf(index);
        return new DirectoryNodeDto
        {
            Ref = new EntryRefDto(catalogId, index),
            Name = name,
            FullPath = fullPath,
            IsDirectory = isDirectory,
            HasChildren = withHasChildren && isDirectory && HasChildDirectory(source, index),
            Size = source.SizeOf(index),
            Modified = source.ModifiedOf(index),
            IsModifiedBad = (flags & Flags.ModifiedBad) == Flags.ModifiedBad,
            IsReparsePoint = (flags & Flags.ReparsePoint) == Flags.ReparsePoint
        };
    }

    // Allocation-free: walks the sibling chain and stops at the first child directory.
    private static bool HasChildDirectory(IEntrySource source, int index)
    {
        for (var c = source.FirstChildOf(index); c != EntryStore.None; c = source.NextSiblingOf(c))
        {
            if (source.IsDirectory(c)) return true;
        }

        return false;
    }

    private void DisposeSlots()
    {
        foreach (var slot in _slots) slot.Dispose();

        _slots = [];
        _roots = [];
    }

    public void Dispose() => DisposeSlots();
}
