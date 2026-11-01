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
    public void ForScanRoot_PatternEqualToRoot_IsDropped()
    {
        var m = Linux("/tmp").ForScanRoot("/tmp");
        Assert.That(m.IsExcluded("/tmp/projects"), Is.False);
    }

    [Test]
    public void ForScanRoot_PatternAboveRoot_IsDropped_ButDeeperPatternsStay()
    {
        var m = Linux("/tmp", "/tmp/projects/cache", "/tmp/other").ForScanRoot("/tmp/projects");
        Assert.That(m.IsExcluded("/tmp/projects/src"), Is.False);
        Assert.That(m.IsExcluded("/tmp/projects/cache"), Is.True);
        Assert.That(m.IsExcluded("/tmp/projects/cache/x"), Is.True);
    }

    [Test]
    public void ForScanRoot_StarPatternMatchingRoot_IsDropped()
    {
        var m = Linux("/home/*").ForScanRoot("/home/jason");
        Assert.That(m.IsExcluded("/home/jason/docs"), Is.False);
    }

    [Test]
    public void ForScanRoot_UnrelatedPatterns_AreKept()
    {
        var m = Linux("/proc").ForScanRoot("/tmp");
        Assert.That(m.IsExcluded("/proc"), Is.True);
    }

    [Test]
    public void None_ExcludesNothing()
    {
        Assert.That(ScanExcludeMatcher.None.IsExcluded("/tmp"), Is.False);
    }

    private static ScanExcludeMatcher Create(string[] user, bool useDefaults, bool isWindows, bool isLinux) =>
        ScanExcludeMatcher.Create(user, useDefaults, isWindows, isLinux);

    [TestCase("/proc")]
    [TestCase("/sys/kernel")]
    [TestCase("/dev")]
    [TestCase("/run/user/1000")]
    [TestCase("/tmp")]
    [TestCase("/var/tmp")]
    [TestCase("/var/cache/apt")]
    [TestCase("/lost+found")]
    [TestCase("/home/alice/.cache")]
    [TestCase("/home/bob/.cache/thumbnails")]
    public void Create_OnLinuxWithDefaults_ExcludesBuiltInPaths(string path)
    {
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: true);

        Assert.That(matcher.IsExcluded(path), Is.True);
    }

    [TestCase("/home/alice")]
    [TestCase("/home/alice/.config")]
    [TestCase("/var/log")]
    [TestCase("/mnt/data/tmp")]
    public void Create_OnLinuxWithDefaults_DoesNotExcludeOtherPaths(string path)
    {
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: true);

        Assert.That(matcher.IsExcluded(path), Is.False);
    }

    [Test]
    public void Create_OnLinuxDefaultsOff_NoBuiltInPaths()
    {
        var matcher = Create([], useDefaults: false, isWindows: false, isLinux: true);

        Assert.That(matcher.IsExcluded("/proc"), Is.False);
        Assert.That(matcher.IsEmpty, Is.True);
    }

    [Test]
    public void Create_OnLinuxDefaultsOff_UserPathsStillApply()
    {
        var matcher = Create(["/data/skip"], useDefaults: false, isWindows: false, isLinux: true);

        Assert.That(matcher.IsExcluded("/data/skip/x"), Is.True);
        Assert.That(matcher.IsExcluded("/tmp"), Is.False);
    }

    [Test]
    public void Create_OnLinuxDefaultsOn_UserPathsAreAdditive()
    {
        var matcher = Create(["/data/skip"], useDefaults: true, isWindows: false, isLinux: true);

        Assert.That(matcher.IsExcluded("/data/skip"), Is.True);
        Assert.That(matcher.IsExcluded("/tmp"), Is.True);
    }

    [Test]
    public void Create_OnWindows_HasNoBuiltInPathsEvenWhenDefaultsOn()
    {
        var matcher = Create([], useDefaults: true, isWindows: true, isLinux: false);

        Assert.That(matcher.IsEmpty, Is.True);
        Assert.That(matcher.IsExcluded("/tmp"), Is.False);
    }

    [Test]
    public void Create_OnMacOs_HasNoBuiltInPathsEvenWhenDefaultsOn()
    {
        var matcher = Create(["/data/skip"], useDefaults: true, isWindows: false, isLinux: false);

        Assert.That(matcher.IsExcluded("/tmp"), Is.False);
        Assert.That(matcher.IsExcluded("/data/skip"), Is.True);
    }

    [Test]
    public void Create_DefaultsAreSubjectToForScanRoot()
    {
        // Scanning /tmp explicitly is the user's intent, so the default for it is dropped for that scan.
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: true).ForScanRoot("/tmp");

        Assert.That(matcher.IsExcluded("/tmp/a"), Is.False);
        Assert.That(matcher.IsExcluded("/proc"), Is.True);
    }

    [Test]
    public void ForScanConfig_NullSection_IsSafe()
    {
        Assert.That(ScanExcludeMatcher.ForScanConfig(null).IsExcluded("/nowhere"), Is.False);
    }

    [Test]
    public void ScanSection_UseDefaultExcludesDefaultsToTrue()
    {
        Assert.That(new cdeLib.Infrastructure.Config.ScanSection().UseDefaultExcludes, Is.True);
    }
}
