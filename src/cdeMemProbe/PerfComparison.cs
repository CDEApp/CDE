using System.Collections.Generic;
using System.Linq;

namespace cdeMemProbe;

public enum DeltaSeverity { Info, Warn, Fail }

public sealed record MetricDelta(string Metric, double? Baseline, double Current, double? PercentChange, DeltaSeverity Severity);

public sealed record ComparisonResult(IReadOnlyList<MetricDelta> Deltas, string? Note)
{
    public bool Failed => Deltas.Any(d => d.Severity == DeltaSeverity.Fail);
    public bool HasWarnings => Deltas.Any(d => d.Severity == DeltaSeverity.Warn);
}

/// <summary>
/// Compares a run against the previous release's baseline. Only <c>.cdex</c> size gates (it is
/// deterministic); peak RSS growth warns; timings never gate.
/// </summary>
public static class PerfComparison
{
    public const double MaxSizeGrowthPercent = 2.0;
    public const double RssWarnGrowthPercent = 15.0;

    public static ComparisonResult Compare(PerfReport? baseline, PerfReport current)
    {
        if (baseline is null)
        {
            return new ComparisonResult(Absolute(current), "No baseline found; absolute numbers only.");
        }

        if (baseline.SchemaVersion != current.SchemaVersion || baseline.FixtureEntries != current.FixtureEntries
            || baseline.Seed != current.Seed)
        {
            return new ComparisonResult(Absolute(current),
                "Baseline used a different schema version, fixture size or seed; not compared.");
        }

        var deltas = new List<MetricDelta>
        {
            Delta(".cdex size (bytes)", baseline.CdexBytes, current.CdexBytes, MaxSizeGrowthPercent, DeltaSeverity.Fail),
            Delta("Peak RSS (bytes)", baseline.PeakRssBytes, current.PeakRssBytes, RssWarnGrowthPercent, DeltaSeverity.Warn),
        };
        if (baseline.ScanMs is { } b && current.ScanMs is { } c)
            deltas.Add(Delta("Scan time (ms)", b, c, null, DeltaSeverity.Info));

        foreach (var q in current.Queries)
        {
            var old = baseline.Queries.FirstOrDefault(x => x.Name == q.Name && x.Pattern == q.Pattern);
            if (old is null) continue;
            deltas.Add(Delta($"{q.Name} cold (ms)", old.ColdMs, q.ColdMs, null, DeltaSeverity.Info));
            deltas.Add(Delta($"{q.Name} warm (ms)", old.WarmMedianMs, q.WarmMedianMs, null, DeltaSeverity.Info));
        }

        return new ComparisonResult(deltas, null);
    }

    private static List<MetricDelta> Absolute(PerfReport r) =>
    [
        new(".cdex size (bytes)", null, r.CdexBytes, null, DeltaSeverity.Info),
        new("Peak RSS (bytes)", null, r.PeakRssBytes, null, DeltaSeverity.Info),
    ];

    private static MetricDelta Delta(string metric, double baseline, double current, double? limitPercent,
        DeltaSeverity overLimit)
    {
        double? pct = baseline > 0 ? (current - baseline) / baseline * 100.0 : null;
        var severity = limitPercent is { } limit && pct is { } p && p > limit ? overLimit : DeltaSeverity.Info;
        return new MetricDelta(metric, baseline, current, pct, severity);
    }
}
