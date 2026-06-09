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
    /// <paramref name="skip"/>/<paramref name="take"/> page the listing.
    /// </summary>
    IReadOnlyList<DirectoryNodeDto> GetChildren(EntryRefDto parent, bool foldersOnly = false,
        int skip = 0, int take = int.MaxValue);

    /// <summary>The root → entry chain (for view-in-tree and go-to-parent).</summary>
    IReadOnlyList<DirectoryNodeDto> GetPath(EntryRefDto entry);

    /// <summary>Resolve an entry reference to its full filesystem path.</summary>
    string ResolveFullPath(EntryRefDto entry);

    /// <summary>Whether the referenced entry currently exists on this machine's filesystem.</summary>
    bool ExistsOnFileSystem(EntryRefDto entry);

    /// <summary>Catalog roots as <see cref="ICommonEntry"/> for in-process consumers (tree build, search).</summary>
    IReadOnlyList<ICommonEntry> Roots { get; }

    /// <summary>The <see cref="IEntrySource"/> backing the catalog with the given id.</summary>
    IEntrySource GetSource(int catalogId);
}
