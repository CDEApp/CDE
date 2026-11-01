namespace cdeLib.Infrastructure.Config;

public class ScanSection
{
    /// <summary>
    /// Absolute folder paths a scan records but does not descend into; '*' matches within one path segment.
    /// </summary>
    public string[] ExcludePaths { get; set; } = [];

    /// <summary>
    /// On Linux, also skip the built-in pseudo/volatile folders (/proc, /sys, /tmp, ...). ExcludePaths add to
    /// them. Ignored on Windows and macOS, which have no built-in defaults. The list lives in code.
    /// </summary>
    public bool UseDefaultExcludes { get; set; } = true;
}
