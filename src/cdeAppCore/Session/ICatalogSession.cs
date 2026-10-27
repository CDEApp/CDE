using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Entities;

namespace cdeAppCore.Session;

/// <summary>
/// An owned, disposable view over a set of loaded catalogs. Replaces the WinForms presenter's
/// implicit <c>List&lt;ICommonEntry&gt;</c> field: it loads catalog sources (zero-copy memory-mapped
/// <c>.cdex</c> readers, or in-memory stores when no <c>.cdex</c> exists), serves the directory tree
/// and per-directory listings as DTOs, resolves entry references to real filesystem paths for the
/// capability-guarded shell actions, and releases every mapping on <see cref="IDisposable.Dispose"/>.
/// In-process it is an app-lifetime singleton; in the API the sidecar process owns one session.
/// </summary>
public interface ICatalogSession : IDisposable
{
    /// <summary>
    /// (Re)load catalogs from the current directory and <paramref name="configPath"/> (one level
    /// down). Releases any previously held mappings first so reloads do not leak. Prefers zero-copy
    /// <c>.cdex</c> sources; falls back to loading <c>.cde</c> trees into in-memory stores.
    /// </summary>
    Task LoadAsync(string configPath, IProgress<CatalogLoadProgress> progress = null,
        CancellationToken cancellationToken = default);

    int CatalogCount { get; }

    /// <summary>Total file + directory entries across all loaded catalogs (status-bar figure).</summary>
    long TotalEntryCount { get; }

    IReadOnlyList<CatalogInfoDto> GetCatalogs();

    /// <summary>
    /// Children of the referenced directory as DTOs. <paramref name="foldersOnly"/> yields only
    /// child directories (tree lazy-expand); otherwise files and directories (directory listing).
    /// <paramref name="skip"/>/<paramref name="take"/> page the listing. With
    /// <paramref name="withHasChildren"/> false, <see cref="DirectoryNodeDto.HasChildren"/> is left
    /// false rather than walking each child folder (for listings that never show an expander).
    /// </summary>
    IReadOnlyList<DirectoryNodeDto> GetChildren(EntryRefDto parent, bool foldersOnly = false,
        int skip = 0, int take = int.MaxValue, bool withHasChildren = true);

    /// <summary>The root → entry chain (for view-in-tree and go-to-parent).</summary>
    IReadOnlyList<DirectoryNodeDto> GetPath(EntryRefDto entry);

    /// <summary>Resolve an entry reference to its full filesystem path.</summary>
    string ResolveFullPath(EntryRefDto entry);

    /// <summary>Whether the referenced entry currently exists on this machine's filesystem.</summary>
    bool ExistsOnFileSystem(EntryRefDto entry);

    /// <summary>Catalog roots as <see cref="ICommonEntry"/> for in-process consumers (tree build, search).</summary>
    IReadOnlyList<ICommonEntry> Roots { get; }

    /// <summary>
    /// The <see cref="IEntrySource"/> backing the catalog with the given id. NOT safe across a rescan swap
    /// (the mapping may be released): long-running consumers use <see cref="AcquireRead"/> instead.
    /// </summary>
    IEntrySource GetSource(int catalogId);

    /// <summary>
    /// Take a read lock on one catalog and return its current source. Hold it for the duration of a search or
    /// listing; a rescan swap waits for every outstanding lease on that catalog. Dispose to release (on the
    /// thread that acquired it).
    /// </summary>
    CatalogReadLease AcquireRead(int catalogId);

    /// <summary>Whether a rescan of the given catalog is currently running.</summary>
    bool IsRescanning(int catalogId);

    /// <summary>Whether any catalog is being rescanned (reload is refused meanwhile).</summary>
    bool AnyRescanning { get; }

    /// <summary>
    /// The working folder: the folder catalogs were last loaded from (full path), where new catalogs are written.
    /// Null until a load has been requested. The single source of truth for both preflight and create.
    /// </summary>
    string CatalogDir { get; }

    /// <summary>Whether any background catalog job (a rescan or a create) is running; a reload is refused meanwhile.</summary>
    bool AnyCatalogJobRunning { get; }

    /// <summary>Everything the New Catalog dialog needs before Create: default name, folder, collisions. Never throws.</summary>
    CreatePreflightResult PreflightCreate(CreatePreflightRequest request);

    /// <summary>
    /// Scan <see cref="CreateCatalogRequest.RootPath"/> into a new <c>.cdex</c> in <see cref="CatalogDir"/> and add it
    /// to the session under a new <c>CatalogId</c> (existing ids are unchanged). The file is written as <c>.tmp</c> and
    /// renamed without overwrite; a failure or cancellation leaves no file and no session change. Throws
    /// <see cref="InvalidCatalogFileNameException"/>, <see cref="CatalogFileExistsException"/>,
    /// <see cref="CatalogFolderNotWritableException"/>, <see cref="NoCatalogDirException"/> or
    /// <see cref="RootNotAccessibleException"/> before scanning, and <see cref="InvalidOperationException"/> during a load.
    /// </summary>
    Task<CreateCatalogResult> CreateCatalogAsync(CreateCatalogRequest request, IProgress<CreateProgress> progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cheap checks before a rescan: catalog exists, none already running, root path reachable.</summary>
    RescanPreflight PreflightRescan(int catalogId);

    /// <summary>
    /// Rescan the catalog's root path in the background and replace the catalog in place (same
    /// <c>CatalogId</c>). The previous data stays usable until the final swap, which takes the catalog's write
    /// lock. Hashes of unchanged files and the description carry over. On failure or cancellation the old file
    /// and in-session data are untouched and no <c>.tmp</c> is left. Throws
    /// <see cref="RootNotAccessibleException"/> or <see cref="RescanAlreadyRunningException"/> before starting.
    /// </summary>
    Task<RescanResult> RescanAsync(int catalogId, IProgress<RescanProgress> progress = null,
        CancellationToken cancellationToken = default);
}
