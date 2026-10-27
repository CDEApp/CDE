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
/// Default <see cref="ICatalogSession"/>. Holds one <see cref="IEntrySource"/> per loaded catalog —
/// a zero-copy <see cref="ColumnarCatalogReader"/> over a memory-mapped <c>.cdex</c> when present,
/// otherwise an in-memory <see cref="EntryStore"/> built from a loaded <c>.cde</c> tree. Catalog id is
/// the index into that list; an entry is addressed by (catalog id, entry index).
/// </summary>
public sealed class CatalogSession : ICatalogSession
{
    private readonly ILoadCatalogService _loadCatalogService;
    private readonly ILogger _logger;

    private List<IEntrySource> _sources = [];
    private List<ICommonEntry> _roots = [];

    public CatalogSession(ILoadCatalogService loadCatalogService, ILogger logger)
    {
        _loadCatalogService = loadCatalogService;
        _logger = logger;
    }

    public int CatalogCount => _sources.Count;

    public long TotalEntryCount =>
        _sources.Sum(s => (long)s.RootFileEntryCount + s.RootDirEntryCount);

    public IReadOnlyList<ICommonEntry> Roots => _roots;

    public IEntrySource GetSource(int catalogId) => _sources[catalogId];

    public async Task LoadAsync(string configPath, IProgress<CatalogLoadProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        DisposeSources();

        // Dedup by full path: the loader scans both "." and configPath, which collapse to the same
        // directory (and thus duplicate catalogs) when the host runs in its catalog directory.
        var cdex = _loadCatalogService.GetColumnarFiles(configPath)
            ?.Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cdex is { Count: > 0 })
        {
            _sources = ReadColumnar(cdex);
        }
        else
        {
            var trees = await _loadCatalogService.LoadRootEntriesAsync(
                configPath,
                (current, total, message) => progress?.Report(new CatalogLoadProgress(current, total, message)),
                cancellationToken);
            _sources = BuildStores(trees);
        }

        _roots = _sources.Select(ICommonEntry (s) => new EntryRef(s, 0)).ToList();
    }

    // Open each .cdex as a zero-copy mmap reader. Unreadable files are skipped (logged).
    private List<IEntrySource> ReadColumnar(IList<string> cdexFiles)
    {
        var sources = new List<IEntrySource>(cdexFiles.Count);
        foreach (var file in cdexFiles)
        {
            try
            {
                sources.Add(new ColumnarCatalogReader(file));
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Skipping unreadable .cdex {File}", file);
            }
        }

        return sources;
    }

    private static List<IEntrySource> BuildStores(List<RootEntry> trees)
    {
        var sources = new List<IEntrySource>(trees?.Count ?? 0);
        if (trees == null) return sources;
        for (var i = 0; i < trees.Count; i++)
        {
            sources.Add(EntryStore.Build(trees[i]));
            trees[i] = null; // release the tree so it can be collected
        }

        return sources;
    }

    public IReadOnlyList<CatalogInfoDto> GetCatalogs()
    {
        var list = new List<CatalogInfoDto>(_sources.Count);
        for (var i = 0; i < _sources.Count; i++)
        {
            var s = _sources[i];
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
                FollowJunctions = s.FollowJunctions
            });
        }

        return list;
    }

    public IReadOnlyList<DirectoryNodeDto> GetChildren(EntryRefDto parent, bool foldersOnly = false,
        int skip = 0, int take = int.MaxValue, bool withHasChildren = true)
    {
        var source = _sources[parent.CatalogId];
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
        var source = _sources[entry.CatalogId];
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
        => _sources[entry.CatalogId].FullPath(entry.EntryIndex);

    public bool ExistsOnFileSystem(EntryRefDto entry)
    {
        var source = _sources[entry.CatalogId];
        var path = source.FullPath(entry.EntryIndex);
        return source.IsDirectory(entry.EntryIndex)
            ? Directory.Exists(path)
            : File.Exists(path);
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

    private void DisposeSources()
    {
        foreach (var source in _sources)
        {
            if (source is IDisposable disposable) disposable.Dispose();
        }

        _sources = [];
        _roots = [];
    }

    public void Dispose() => DisposeSources();
}
