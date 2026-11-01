using System.Collections.Generic;

namespace cde.ScanProgress;

/// <summary>
/// The end-of-scan "skipped N folders" report (#169): excluded folders and access-denied folders, counted separately.
/// </summary>
public static class ScanSkipSummary
{
    /// <summary>
    /// Lines to print after a scan. Empty when nothing was skipped; with <paramref name="verbose"/> the
    /// access-denied paths follow the summary line.
    /// </summary>
    public static IReadOnlyList<string> Lines(int excluded, int accessDenied, IEnumerable<string> accessDeniedPaths,
        bool verbose)
    {
        var lines = new List<string>();
        var total = excluded + accessDenied;
        if (total == 0)
        {
            return lines;
        }

        lines.Add($"skipped {total} {(total == 1 ? "folder" : "folders")}: {excluded} excluded, {accessDenied} access denied");
        if (verbose && accessDeniedPaths != null)
        {
            foreach (var path in accessDeniedPaths)
            {
                lines.Add($"  {path}");
            }
        }

        return lines;
    }
}
