namespace cdeAppCore.Dtos;

/// <summary>
/// A serializable catalog summary — one row of the catalog-list view, and the source of the status
/// bar's "catalogs loaded / total entries" figures. Mirrors the per-catalog metadata the WinForms
/// catalog list rendered from <c>IEntrySource</c>.
/// </summary>
public sealed class CatalogInfoDto
{
    /// <summary>Index of this catalog in the loaded session (the <see cref="EntryRefDto.CatalogId"/>).</summary>
    public int CatalogId { get; set; }

    public string RootPath { get; set; }
    public string VolumeName { get; set; }
    public uint DirEntryCount { get; set; }
    public uint FileEntryCount { get; set; }
    public string DriveLetterHint { get; set; }
    public long RootSize { get; set; }
    public long AvailSpace { get; set; }
    public long TotalSpace { get; set; }
    public long ScanStartUtcTicks { get; set; }
    public long ScanEndUtcTicks { get; set; }
    public string ActualFileName { get; set; }
    public string DefaultFileName { get; set; }
    public string Description { get; set; }
}
