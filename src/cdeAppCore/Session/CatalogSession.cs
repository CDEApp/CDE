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
                Description = s.Description
            });
        }

        return list;
    }

    public IReadOnlyList<DirectoryNodeDto> GetChildren(EntryRefDto parent, bool foldersOnly = false,
        int skip = 0, int take = int.MaxValue)
    {
        var source = _sources[parent.CatalogId];
        var parentRef = new EntryRef(source, parent.EntryIndex);

        IEnumerable<ICommonEntry> children = parentRef.Children ?? [];
        if (foldersOnly)
        {
            children = children.Where(c => c.IsDirectory);
        }

        if (skip > 0) children = children.Skip(skip);
        if (take != int.MaxValue) children = children.Take(take);

        return children.Select(c => ToNodeDto(parent.CatalogId, c)).ToList();
    }

    public IReadOnlyList<DirectoryNodeDto> GetPath(EntryRefDto entry)
    {
        var source = _sources[entry.CatalogId];
        var entryRef = new EntryRef(source, entry.EntryIndex);
        return entryRef.GetListFromRoot()
            .Select(e => ToNodeDto(entry.CatalogId, e))
            .ToList();
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

    private static DirectoryNodeDto ToNodeDto(int catalogId, ICommonEntry entry)
    {
        var index = (entry as EntryRef)?.Index ?? 0;
        return new DirectoryNodeDto
        {
            Ref = new EntryRefDto(catalogId, index),
            Name = entry.Path,
            FullPath = entry.FullPath,
            IsDirectory = entry.IsDirectory,
            HasChildren = entry.IsDirectory && (entry.Children?.Any(c => c.IsDirectory) ?? false),
            Size = entry.Size,
            Modified = entry.Modified,
            IsModifiedBad = entry.IsModifiedBad,
            IsReparsePoint = entry.IsReparsePoint
        };
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
