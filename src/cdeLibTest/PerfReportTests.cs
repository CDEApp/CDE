using System;
using System.Linq;
using cdeMemProbe;
using FluentAssertions;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>The release performance report (#189): comparison gating, perf.json round trip, Readme block.</summary>
[TestFixture]
public class PerfReportTests
{
    private static PerfReport Report(long cdexBytes = 1_000_000, long rssBytes = 100_000_000, double? scanMs = 5000,
        double warmMs = 10) => new()
    {
        Commit = "0123456789abcdef",
        Os = "Ubuntu 24.04",
        FixtureEntries = 1_000_000,
        Seed = 42,
        ScanMs = scanMs,
        CdexBytes = cdexBytes,
        PeakRssBytes = rssBytes,
        ColdCacheDropped = true,
        Queries = [new QueryTiming("name substring", "name", "Report_00", 50, warmMs, 123)],
    };

    [Test]
    public void NoBaseline_ReportsAbsoluteNumbersAndDoesNotFail()
    {
        var result = PerfComparison.Compare(null, Report());

        result.Failed.Should().BeFalse();
        result.Note.Should().Contain("No baseline");
        result.Deltas.Should().NotBeEmpty();
    }

    [Test]
    public void CdexSizeGrowthOverTwoPercent_Fails()
    {
        var result = PerfComparison.Compare(Report(cdexBytes: 1_000_000), Report(cdexBytes: 1_021_000));

        result.Failed.Should().BeTrue();
    }

    [Test]
    public void CdexSizeGrowthWithinTwoPercent_Passes()
    {
        var result = PerfComparison.Compare(Report(cdexBytes: 1_000_000), Report(cdexBytes: 1_019_000));

        result.Failed.Should().BeFalse();
    }

    [Test]
    public void CdexSizeShrink_Passes()
    {
        PerfComparison.Compare(Report(cdexBytes: 1_000_000), Report(cdexBytes: 800_000)).Failed.Should().BeFalse();
    }

    [Test]
    public void RssGrowthOverFifteenPercent_WarnsButDoesNotFail()
    {
        var result = PerfComparison.Compare(Report(rssBytes: 100_000_000), Report(rssBytes: 120_000_000));

        result.Failed.Should().BeFalse();
        result.HasWarnings.Should().BeTrue();
    }

    [Test]
    public void TimingRegressions_NeverFailOrWarn()
    {
        var result = PerfComparison.Compare(Report(scanMs: 1000, warmMs: 1), Report(scanMs: 9000, warmMs: 90));

        result.Failed.Should().BeFalse();
        result.HasWarnings.Should().BeFalse();
        result.Deltas.Should().Contain(d => d.Metric == "name substring warm (ms)");
    }

    [Test]
    public void BaselineWithDifferentFixture_IsNotCompared()
    {
        var other = Report() with { FixtureEntries = 10_000_000, CdexBytes = 1 };

        var result = PerfComparison.Compare(other, Report(cdexBytes: 5_000_000));

        result.Failed.Should().BeFalse();
        result.Note.Should().Contain("not compared");
    }

    [Test]
    public void Json_RoundTripsAndIsVersioned()
    {
        var original = Report(scanMs: null);

        var json = original.ToJson();
        var copy = PerfReport.FromJson(json);

        json.Should().Contain("\"schemaVersion\": 1");
        copy.ScanMs.Should().BeNull();
        copy.Queries.Single().Pattern.Should().Be("Report_00");
        copy.CdexBytes.Should().Be(original.CdexBytes);
    }

    [Test]
    public void ReplaceBlock_SwapsOnlyTheMarkedRegion()
    {
        var readme = $"before\n{PerfMarkdown.BeginMarker}\nold\n{PerfMarkdown.EndMarker}\nafter\n";

        var updated = PerfMarkdown.ReplaceBlock(readme, PerfMarkdown.ReadmeBlock(Report()));

        updated.Should().StartWith("before\n").And.EndWith("\nafter\n");
        updated.Should().NotContain("\nold\n");
        updated.Should().Contain("GitHub ubuntu runner");
    }

    [Test]
    public void ReplaceBlock_WithoutMarkers_Throws()
    {
        var act = () => PerfMarkdown.ReplaceBlock("no markers", "x");

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ReadmeBlock_SaysScanTimeIsNotAvailableWhenWrittenDirectly()
    {
        PerfMarkdown.ReadmeBlock(Report(scanMs: null)).Should().Contain("n/a");
    }

    [TestCase(new[] { 3.0, 1.0, 2.0 }, 2.0)]
    [TestCase(new[] { 4.0, 1.0, 2.0, 3.0 }, 2.5)]
    public void Median_HandlesOddAndEvenCounts(double[] values, double expected)
    {
        ReportRunner.Median(values).Should().Be(expected);
    }
}
