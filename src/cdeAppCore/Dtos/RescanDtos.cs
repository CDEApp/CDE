using System;

namespace cdeAppCore.Dtos;

/// <summary>Rescan progress. <see cref="Phase"/> is <c>scanning</c>, <c>writing</c> or <c>swapping</c>.</summary>
public readonly record struct RescanProgress(int CatalogId, string Phase, int EntriesScanned, string CurrentPath);

/// <summary>Final outcome of a successful rescan (the SSE <c>done</c> payload).</summary>
public sealed class RescanResult
{
    public int CatalogId { get; set; }
    public string RootPath { get; set; }
    public long Entries { get; set; }

    /// <summary>The file the new catalog was saved to (a <c>.cdex</c>; differs from the old path for a legacy <c>.cde</c>).</summary>
    public string SavedPath { get; set; }

    /// <summary>Folders skipped by the scan's exclude patterns (#170). Scan-time only: not stored in the catalog.</summary>
    public int ExcludedFolderCount { get; set; }

    /// <summary>Folders the scan could not read (access denied) (#170). Scan-time only: not stored in the catalog.</summary>
    public int AccessDeniedFolderCount { get; set; }
}

/// <summary>The SSE <c>error</c> payload. <see cref="Code"/> is a stable machine-readable reason.</summary>
public sealed class RescanError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string RootPath { get; set; }
}

/// <summary>Result of the cheap checks done before a rescan is started.</summary>
public enum RescanPreflight
{
    Ready,
    UnknownCatalog,
    AlreadyRunning,
    RootNotAccessible,

    /// <summary>The catalog has no known source file to save over, so it cannot be rescanned.</summary>
    NoSourceFile,

    /// <summary>A catalog load is running (loads and rescans are mutually exclusive).</summary>
    Busy,

    /// <summary>The catalog failed an earlier swap and could not be re-opened; reload to recover.</summary>
    Unavailable
}

/// <summary>The catalog has no source file path, so there is nowhere to save a rescan.</summary>
public sealed class NoSourceFileException(int catalogId)
    : InvalidOperationException($"Catalog {catalogId} has no source file to save a rescan to.")
{
    public int CatalogId { get; } = catalogId;
}

/// <summary>The catalog's root path does not exist or cannot be read. Nothing was changed.</summary>
public sealed class RootNotAccessibleException(string rootPath)
    : Exception($"{rootPath} can't be reached (drive disconnected, path moved, or no permission). The catalog was left unchanged.")
{
    public string RootPath { get; } = rootPath;
}

/// <summary>A rescan of this catalog is already running (one per catalog).</summary>
public sealed class RescanAlreadyRunningException(int catalogId)
    : InvalidOperationException($"A rescan of catalog {catalogId} is already running.")
{
    public int CatalogId { get; } = catalogId;
}
