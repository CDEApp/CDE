using System.Globalization;
using System.Collections.Generic;

namespace cde.ScanProgress;

/// <summary>
/// The end-of-scan "skipped N folders" report (#169): excluded, access-denied and otherwise unreadable folders, counted separately.
/// </summary>
public static class ScanSkipSummary
{
    /// <summary>
    /// Lines to print after a scan. Empty when nothing was skipped; with <paramref name="verbose"/> the
    /// unreadable paths (access denied or an IO error) follow the summary line. Zero parts are omitted.
    /// </summary>
    public static IReadOnlyList<string> Lines(int excluded, int accessDenied, int unreadable,
        IEnumerable<string> unreadablePaths, bool verbose)
    {
        var lines = new List<string>();
        var total = excluded + accessDenied + unreadable;
        if (total == 0)
        {
            return lines;
        }

        var parts = new List<string>(3);
        if (excluded > 0) parts.Add($"{excluded.ToString(CultureInfo.InvariantCulture)} excluded");
        if (accessDenied > 0) parts.Add($"{accessDenied.ToString(CultureInfo.InvariantCulture)} access denied");
        if (unreadable > 0) parts.Add($"{unreadable.ToString(CultureInfo.InvariantCulture)} unreadable");

        lines.Add($"skipped {total.ToString(CultureInfo.InvariantCulture)} {(total == 1 ? "folder" : "folders")}: {string.Join(", ", parts)}");
        if (verbose && unreadablePaths != null)
        {
            foreach (var path in unreadablePaths)
            {
                lines.Add($"  {path}");
            }
        }

        return lines;
    }
}
