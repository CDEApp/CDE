using System.Collections.Generic;
using System.Linq;
using cdeLib.Entities;

namespace cde.ScanProgress;

/// <summary>Console warnings for per-run <c>--exclude</c> patterns that will be ignored (Serilog alone is below the default log level).</summary>
public static class ScanExcludeWarnings
{
    public static IReadOnlyList<string> Lines(IEnumerable<string> excludePaths, bool isWindows)
    {
        return ScanExcludeMatcher.InvalidPatterns(excludePaths, isWindows)
            .Select(p => $"Warning: --exclude \"{p}\" ignored, it is not an absolute path or a bare folder name.")
            .ToList();
    }
}
