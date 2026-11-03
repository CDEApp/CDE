using System.IO;

namespace cdeLib;

/// <summary>How a catalog is named when a search or a result row needs its file name.</summary>
public static class CatalogNames
{
    /// <summary>
    /// The file name on disk, with its extension, not the name stored inside the catalog, which goes stale when the file
    /// is renamed or moved. <paramref name="storedName"/> is only the fallback when the path is unknown.
    /// </summary>
    public static string FileName(string diskPath, string storedName) =>
        string.IsNullOrEmpty(diskPath) ? storedName ?? string.Empty : Path.GetFileName(diskPath);
}
