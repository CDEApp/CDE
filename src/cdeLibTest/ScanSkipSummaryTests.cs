using System.Linq;
using cde.ScanProgress;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class ScanSkipSummaryTests
{
    private static readonly string[] Denied = { @"C:\a\locked", @"C:\b\locked" };

    [Test]
    public void Lines_NothingSkipped_PrintsNothing()
    {
        Assert.That(ScanSkipSummary.Lines(0, 0, null, verbose: true), Is.Empty);
    }

    [Test]
    public void Lines_BothCounts_ShowsTotalAndSplit()
    {
        var lines = ScanSkipSummary.Lines(9, 5, null, verbose: false);

        Assert.That(lines, Is.EqualTo(new[] { "skipped 14 folders: 9 excluded, 5 access denied" }));
    }

    [Test]
    public void Lines_SingleFolder_IsSingular()
    {
        var lines = ScanSkipSummary.Lines(1, 0, null, verbose: false);

        Assert.That(lines, Is.EqualTo(new[] { "skipped 1 folder: 1 excluded, 0 access denied" }));
    }

    [Test]
    public void Lines_NotVerbose_DoesNotListAccessDeniedPaths()
    {
        var lines = ScanSkipSummary.Lines(0, 2, Denied, verbose: false);

        Assert.That(lines, Has.Count.EqualTo(1));
    }

    [Test]
    public void Lines_Verbose_ListsAccessDeniedPaths()
    {
        var lines = ScanSkipSummary.Lines(0, 2, Denied, verbose: true);

        Assert.That(lines[0], Is.EqualTo("skipped 2 folders: 0 excluded, 2 access denied"));
        Assert.That(lines.Skip(1).Select(l => l.Trim()), Is.EqualTo(Denied));
    }
}
