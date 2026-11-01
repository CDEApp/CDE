using System;
using System.IO;

namespace cdeMemProbe;

/// <summary>The <c>--compare</c> and <c>--readme</c> modes, which post-process a perf.json.</summary>
public static class PerfCommands
{
    /// <summary>Exit code of <c>--compare</c> when the .cdex size gate fails; 1 is a usage error.</summary>
    public const int GateFailedExitCode = 3;

    public static int Compare(Func<string, string?> flag)
    {
        var currentPath = flag("--current");
        if (string.IsNullOrEmpty(currentPath) || !File.Exists(currentPath))
        {
            Console.Error.WriteLine("--compare requires --current <perf.json>");
            return 1;
        }

        var baselinePath = flag("--baseline");
        var baseline = !string.IsNullOrEmpty(baselinePath) && File.Exists(baselinePath)
            ? PerfReport.Load(baselinePath)
            : null;
        var current = PerfReport.Load(currentPath);

        var result = PerfComparison.Compare(baseline, current);
        var markdown = PerfMarkdown.SummaryTable(current, result);
        var markdownPath = flag("--markdown");
        if (!string.IsNullOrEmpty(markdownPath)) File.WriteAllText(markdownPath, markdown);
        Console.WriteLine(markdown);

        foreach (var d in result.Deltas)
        {
            // GitHub Actions turns these into annotations; elsewhere they are just readable lines.
            if (d.Severity == DeltaSeverity.Fail)
                Console.Error.WriteLine($"::error::{d.Metric} grew {d.PercentChange:0.0}% (limit {PerfComparison.MaxSizeGrowthPercent}%)");
            else if (d.Severity == DeltaSeverity.Warn)
                Console.Error.WriteLine($"::warning::{d.Metric} grew {d.PercentChange:0.0}% (warn above {PerfComparison.RssWarnGrowthPercent}%)");
        }

        return result.Failed ? GateFailedExitCode : 0;
    }

    public static int Readme(Func<string, string?> flag)
    {
        var currentPath = flag("--current");
        var readmePath = flag("--file");
        if (string.IsNullOrEmpty(currentPath) || string.IsNullOrEmpty(readmePath) || !File.Exists(currentPath)
            || !File.Exists(readmePath))
        {
            Console.Error.WriteLine("--readme requires --current <perf.json> and --file <Readme.md>");
            return 1;
        }

        var updated = PerfMarkdown.ReplaceBlock(File.ReadAllText(readmePath),
            PerfMarkdown.ReadmeBlock(PerfReport.Load(currentPath)));
        File.WriteAllText(readmePath, updated);
        Console.Error.WriteLine($"Updated {readmePath}");
        return 0;
    }
}
