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
}
