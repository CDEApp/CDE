namespace cdeLib.Infrastructure.Config;

public class ScanSection
{
    /// <summary>
    /// Absolute folder paths a scan records but does not descend into; '*' matches within one path segment.
    /// </summary>
    public string[] ExcludePaths { get; set; } = [];

    /// <summary>
    /// On Linux and macOS, also skip that OS's built-in pseudo/volatile folders (Linux: /proc, /sys, /tmp, ...;
    /// macOS: /dev, /.Spotlight-V100, /.fseventsd). ExcludePaths add to them. Ignored on Windows, which has no
    /// built-in defaults. The list lives in code.
    /// </summary>
    public bool UseDefaultExcludes { get; set; } = true;

    /// <summary>
    /// A catalog scanned longer ago than this many days is marked stale in cde-tauri. 0 turns the marker off;
    /// a negative value is treated as 0.
    /// </summary>
    public int StaleAfterDays { get; set; } = 30;

    /// <summary>
    /// When true, cde-tauri rescans the stale catalogs once, after the first catalog load of a session.
    /// </summary>
    public bool RescanStaleOnStartup { get; set; }
}
