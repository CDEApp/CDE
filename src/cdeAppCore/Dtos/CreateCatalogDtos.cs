using System;
using System.IO;

namespace cdeAppCore.Dtos;

/// <summary>Body of <c>POST /catalogs</c>: scan <see cref="RootPath"/> into a new <c>&lt;FileName&gt;.cdex</c> in the working folder.</summary>
public sealed class CreateCatalogRequest
{
    public string RootPath { get; set; }

    /// <summary>File name without the <c>.cdex</c> extension (it is appended). Null/empty uses the default name.</summary>
    public string FileName { get; set; }

    public string Description { get; set; }
    public bool FollowJunctions { get; set; }
}

/// <summary>Progress of a create. <see cref="Phase"/> is <c>scanning</c>, <c>writing</c> or <c>saving</c>.</summary>
public readonly record struct CreateProgress(string Phase, int EntriesScanned, string CurrentPath);

/// <summary>Final outcome of a successful create (the SSE <c>done</c> payload).</summary>
public sealed class CreateCatalogResult
{
    /// <summary>The id of the new catalog; existing catalogs keep theirs.</summary>
    public int CatalogId { get; set; }

    public string RootPath { get; set; }

    /// <summary>Files + directories in the new catalog (root excluded, as in the status-bar total).</summary>
    public long Entries { get; set; }

    /// <summary>Full path of the written <c>.cdex</c>.</summary>
    public string SavedPath { get; set; }
}

/// <summary>The SSE <c>error</c> payload, or the JSON body of a refusal. <see cref="Code"/> is a stable reason.</summary>
public sealed class CreateCatalogError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string RootPath { get; set; }

    /// <summary>For <c>file-exists</c>: the loaded catalog backed by that file (Rescan existing), else null.</summary>
    public int? ExistingCatalogId { get; set; }
}

/// <summary>Body of <c>POST /catalogs/preflight</c>.</summary>
public sealed class CreatePreflightRequest
{
    public string RootPath { get; set; }

    /// <summary>The name being typed (no extension). Null/empty: check the default name.</summary>
    public string FileName { get; set; }
}

/// <summary>What the New Catalog dialog needs to know before Create. All fields are best-effort and never throw.</summary>
public sealed class CreatePreflightResult
{
    /// <summary>cde's default name for the root (<c>{hint}-{volume}[-{safePath}]</c> or UNC form), without extension.</summary>
    public string DefaultFileName { get; set; }

    /// <summary>The name that was checked (the requested one, else the default), without extension.</summary>
    public string FileName { get; set; }

    /// <summary>Why <see cref="FileName"/> can't be used (separators, invalid characters, ...), or null.</summary>
    public string FileNameError { get; set; }

    /// <summary>The working folder the file would be written to (null when no folder is known yet).</summary>
    public string CatalogDir { get; set; }

    /// <summary>Full path the catalog would be written to (<c>CatalogDir\FileName.cdex</c>); null if not computable.</summary>
    public string TargetPath { get; set; }

    /// <summary>That file already exists (or another create is about to write it). Never overwritten.</summary>
    public bool Exists { get; set; }

    /// <summary>When <see cref="Exists"/>: the loaded catalog backed by that file, so the UI can offer Rescan existing.</summary>
    public int? ExistingCatalogId { get; set; }

    /// <summary>The working folder exists and a file can be created in it.</summary>
    public bool Writable { get; set; }

    /// <summary>Loaded catalogs with the same root (a warning, not an error).</summary>
    public int[] SameRootCatalogIds { get; set; } = [];

    /// <summary>The root exists and can be listed.</summary>
    public bool RootAccessible { get; set; }

    /// <summary>A catalog load is running (loads and creates are mutually exclusive).</summary>
    public bool Busy { get; set; }
}

/// <summary>The file name is empty or has path separators / invalid characters.</summary>
public sealed class InvalidCatalogFileNameException(string reason) : ArgumentException(reason);

/// <summary>The target <c>.cdex</c> exists (or appeared before the rename). Nothing was overwritten.</summary>
public sealed class CatalogFileExistsException(string path, int? existingCatalogId)
    : IOException($"{path} already exists; a catalog is never overwritten.")
{
    public string Path { get; } = path;
    public int? ExistingCatalogId { get; } = existingCatalogId;
}

/// <summary>The working folder is missing or can't be written to.</summary>
public sealed class CatalogFolderNotWritableException(string dir)
    : IOException($"The catalog folder {dir} doesn't exist or can't be written to.")
{
    public string Dir { get; } = dir;
}

/// <summary>A catalog load is running; loads, rescans and creates are mutually exclusive with it.</summary>
public sealed class CatalogJobBusyException(string message) : InvalidOperationException(message);

/// <summary>No catalog working folder is known yet (nothing has been loaded).</summary>
public sealed class NoCatalogDirException()
    : InvalidOperationException("There is no catalog folder to save to; open or reload a catalog folder first.");
