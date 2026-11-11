using System;
using System.Text.Json.Serialization;

namespace cdeAppCore.Dtos;

/// <summary>
/// One materialized search-result row. Carries the raw values both frontends need (size/date are
/// unformatted so each frontend renders with its own <c>EntryFormatter</c>), the resolved full path
/// and parent path, the owning catalog's display name, and the <see cref="EntryRefDto"/> used to run
/// shell actions against the entry. Only the visible page of rows is ever materialized to this DTO;
/// the catalog itself never serializes.
/// </summary>
public sealed class SearchResultRow
{
    public EntryRefDto Ref { get; set; }

    /// <summary>The entry's own name (the WinForms result "Name" column).</summary>
    public string Name { get; set; }

    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public bool IsModifiedBad { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsReparsePoint { get; set; }

    /// <summary>Full path of the entry itself.</summary>
    public string FullPath { get; set; }

    /// <summary>Full path of the entry's parent directory (the WinForms result "Path" column).</summary>
    public string ParentPath { get; set; }

    /// <summary>The owning catalog's display file name (the WinForms result "Catalog" column).</summary>
    public string CatalogName { get; set; }

    /// <summary>
    /// The file's full hash as 32 lowercase hex digits (the <c>xxh128sum</c> form that <c>hash:</c> takes), or null
    /// when the catalog holds no usable full hash for it: a folder, an unhashed file, or only a partial or legacy hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ContentHash { get; set; }

    /// <summary>
    /// How many OTHER files in the loaded catalogs have the same <see cref="ContentHash"/>; 0 for a unique file. Null
    /// when <see cref="ContentHash"/> is null or the query did not set <see cref="SearchQuery.IncludeDuplicateCounts"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DuplicateCount { get; set; }
}
