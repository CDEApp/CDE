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
        Assert.That(ScanSkipSummary.Lines(0, 0, 0, null, verbose: true), Is.Empty);
    }

    [Test]
    public void Lines_AllThreeCounts_ShowsTotalAndSplit()
    {
        var lines = ScanSkipSummary.Lines(9, 3, 2, null, verbose: false);

        Assert.That(lines, Is.EqualTo(new[] { "skipped 14 folders: 9 excluded, 3 access denied, 2 unreadable" }));
    }

    [Test]
    public void Lines_ZeroParts_AreOmitted()
    {
        Assert.That(ScanSkipSummary.Lines(9, 0, 0, null, verbose: false),
            Is.EqualTo(new[] { "skipped 9 folders: 9 excluded" }));
        Assert.That(ScanSkipSummary.Lines(0, 4, 0, null, verbose: false),
            Is.EqualTo(new[] { "skipped 4 folders: 4 access denied" }));
        Assert.That(ScanSkipSummary.Lines(5, 0, 1, null, verbose: false),
            Is.EqualTo(new[] { "skipped 6 folders: 5 excluded, 1 unreadable" }));
    }

    [Test]
    public void Lines_SingleFolder_IsSingular()
    {
        var lines = ScanSkipSummary.Lines(1, 0, 0, null, verbose: false);

        Assert.That(lines, Is.EqualTo(new[] { "skipped 1 folder: 1 excluded" }));
    }

    [Test]
    public void Lines_NotVerbose_DoesNotListPaths()
    {
        var lines = ScanSkipSummary.Lines(0, 2, 0, Denied, verbose: false);

        Assert.That(lines, Has.Count.EqualTo(1));
    }

    [Test]
    public void Lines_Verbose_ListsUnreadablePaths()
    {
        var lines = ScanSkipSummary.Lines(0, 1, 1, Denied, verbose: true);

        Assert.That(lines[0], Is.EqualTo("skipped 2 folders: 1 access denied, 1 unreadable"));
        Assert.That(lines.Skip(1).Select(l => l.Trim()), Is.EqualTo(Denied));
    }
}
