namespace cdeLib.Infrastructure.Config;

public class ScanSection
{
    /// <summary>
    /// Absolute folder paths a scan records but does not descend into; '*' matches within one path segment.
    /// </summary>
    public string[] ExcludePaths { get; set; } = [];
}
