using cdeLib.Entities;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// The scan exclude matcher: absolute patterns, per-segment '*', OS-specific case rules.
/// The OS is injected so Windows and Linux rules are both checked on any host.
/// </summary>
[TestFixture]
public class ScanExcludeMatcherTests
{
    private static ScanExcludeMatcher Linux(params string[] patterns) => new(patterns, isWindows: false);
    private static ScanExcludeMatcher Windows(params string[] patterns) => new(patterns, isWindows: true);

    [Test]
    public void IsExcluded_ExactFolder_Matches()
    {
        Assert.That(Linux("/tmp").IsExcluded("/tmp"), Is.True);
    }

    [Test]
    public void IsExcluded_FolderUnderExcluded_Matches()
    {
        Assert.That(Linux("/tmp").IsExcluded("/tmp/a/b"), Is.True);
    }

    [Test]
    public void IsExcluded_PatternDoesNotMatchSameNameDeeper()
    {
        Assert.That(Linux("/tmp").IsExcluded("/mnt/data/tmp"), Is.False);
    }

    [Test]
    public void IsExcluded_SiblingWithSharedPrefix_DoesNotMatch()
    {
        Assert.That(Linux("/tmp").IsExcluded("/tmpfiles"), Is.False);
    }

    [Test]
    public void IsExcluded_StarMatchesWithinOneSegment()
    {
        var m = Linux("/home/*/.cache");
        Assert.That(m.IsExcluded("/home/jason/.cache"), Is.True);
        Assert.That(m.IsExcluded("/home/jason/.cache/pip"), Is.True);
        Assert.That(m.IsExcluded("/home/jason/other/.cache"), Is.False);
        Assert.That(m.IsExcluded("/home/.cache"), Is.False);
    }

    [Test]
    public void IsExcluded_StarInsideSegment_Matches()
    {
        var m = Linux("/var/log/app-*-old");
        Assert.That(m.IsExcluded("/var/log/app-x-old"), Is.True);
        Assert.That(m.IsExcluded("/var/log/app-x-new"), Is.False);
    }

    [Test]
    public void IsExcluded_TrailingSlashOnPatternIgnored()
    {
        Assert.That(Linux("/tmp/").IsExcluded("/tmp/x"), Is.True);
    }

    [Test]
    public void IsExcluded_Linux_IsCaseSensitive()
    {
        Assert.That(Linux("/Tmp").IsExcluded("/tmp"), Is.False);
    }

    [Test]
    public void IsExcluded_Windows_IsCaseInsensitiveAndSeparatorAgnostic()
    {
        var m = Windows(@"C:\Users\*\AppData\Local\Temp");
        Assert.That(m.IsExcluded(@"c:\users\Jason\appdata\local\temp\x"), Is.True);
        Assert.That(Windows("C:/Temp").IsExcluded(@"C:\temp"), Is.True);
    }

    [Test]
    public void IsExcluded_Windows_DifferentDriveDoesNotMatch()
    {
        Assert.That(Windows(@"C:\Temp").IsExcluded(@"D:\Temp"), Is.False);
    }

    [Test]
    public void Constructor_BlankAndNonAbsolutePatterns_AreSkipped()
    {
        var m = Linux("", "   ", null, "tmp", "cache/x", "*", "./rel");
        Assert.That(m.IsEmpty, Is.True);
        Assert.That(m.IsExcluded("/tmp"), Is.False);
    }

    [Test]
    public void Constructor_BadPatternDoesNotStopGoodOnes()
    {
        var m = Linux("nope", "/tmp");
        Assert.That(m.IsEmpty, Is.False);
        Assert.That(m.IsExcluded("/tmp"), Is.True);
    }

    [Test]
    public void None_ExcludesNothing()
    {
        Assert.That(ScanExcludeMatcher.None.IsExcluded("/tmp"), Is.False);
    }
}
