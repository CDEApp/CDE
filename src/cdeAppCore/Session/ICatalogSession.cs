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

    /// <summary>Whether any background catalog job (a rescan, a create or a migration) is running; a reload is refused meanwhile.</summary>
    bool AnyCatalogJobRunning { get; }

    /// <summary>
    /// The legacy <c>.cde</c> catalogs the last load found and did not load (no <c>.cdex</c> beside them). Empty when
    /// the session loads legacy catalogs itself (<see cref="CatalogSession.LoadLegacyCatalogs"/>).
    /// </summary>
    IReadOnlyList<LegacyCatalogDto> LegacyCatalogs { get; }

    /// <summary>
    /// Convert the writable <see cref="LegacyCatalogs"/> to <c>.cdex</c>, one at a time, renaming each source to
    /// <c>.cde.migrated</c> (see <c>CatalogMigrator</c>). A failed catalog is reported and stays listed; converted ones
    /// leave the list. Loads nothing: reload to see the new catalogs. Throws <see cref="CatalogJobBusyException"/>
    /// while a load or another catalog job runs; on cancel, the catalogs already converted stay converted.
    /// </summary>
    Task<MigrateResult> MigrateLegacyCatalogsAsync(IProgress<MigrateProgress> progress = null,
        CancellationToken cancellationToken = default);

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

    /// <summary>Longest description <see cref="SetDescriptionAsync"/> accepts (after trimming).</summary>
    public const int MaxDescriptionLength = 1000;

    /// <summary>
    /// Replace the catalog's description (trimmed; empty clears it). Only the metadata of the <c>.cdex</c> is
    /// rewritten, then swapped in like a rescan, so the catalog keeps its <c>CatalogId</c> and every entry.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for an unknown catalog, <see cref="ArgumentException"/>
    /// for a description over <see cref="MaxDescriptionLength"/>, <see cref="RescanAlreadyRunningException"/>
    /// while a rescan or another edit of this catalog runs, <see cref="NoSourceFileException"/> when the catalog
    /// has no file, and <see cref="NotSupportedException"/> for a legacy <c>.cde</c> (migrate it first).
    /// </summary>
    Task SetDescriptionAsync(int catalogId, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap checks before a hash run of <paramref name="catalogIds"/>: null when it can start, otherwise why not, as
    /// one of <see cref="JobErrorCodes"/>: <c>invalid-request</c> (no catalog), <c>invalid-filter</c> (the message is
    /// the one the search bar shows), <c>busy</c>, <c>unknown-catalog</c>, <c>already-running</c>,
    /// <c>catalog-unavailable</c>, <c>legacy-catalog</c>, <c>no-source-file</c> or <c>root-not-accessible</c>.
    /// </summary>
    JobError PreflightHash(IReadOnlyList<int> catalogIds, HashOptions options);

    /// <summary>
    /// What <see cref="HashAsync"/> with the same arguments would read, counted from the loaded catalogs under read
    /// leases without reading any file. Throws <see cref="HashRefusedException"/> for <c>invalid-request</c>,
    /// <c>invalid-filter</c>, <c>busy</c>, <c>unknown-catalog</c>, <c>catalog-unavailable</c> or <c>legacy-catalog</c>.
    /// It does not need the catalogs' drives, and it works while another job runs on them.
    /// </summary>
    HashPreflightResult PreviewHash(IReadOnlyList<int> catalogIds, HashOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hash the files of <paramref name="catalogIds"/> as one run (file sizes are compared across the whole set, as
    /// <c>cde hash</c> does over a folder of catalogs) and replace each catalog in place with its hashed copy (same
    /// <c>CatalogId</c>). The run only adds or upgrades hashes. Each catalog stays searchable until its swap, which
    /// takes that catalog's write lock. A cancel before the first swap leaves every file as it was and no
    /// <c>.tmp</c>; from the first swap on the run is not cancellable. No rescan, edit or reload of a catalog in the
    /// set can run meanwhile. Throws <see cref="HashRefusedException"/> for the refusals of
    /// <see cref="PreflightHash"/>, and <c>cdeLib.Hashing.HashFailedException</c> when a pass stopped on an
    /// unexpected error or a catalog could not be saved (the hashes of the other catalogs are still saved).
    /// </summary>
    Task<HashResult> HashAsync(IReadOnlyList<int> catalogIds, HashOptions options,
        IProgress<HashProgress> progress = null, CancellationToken cancellationToken = default);
}
