using System;
using System.IO;
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
        var m = Linux("", "   ", null, "cache/x", "*", "**", ".", "..", "./rel");
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

    private static ScanExcludeMatcher ForHost(params string[] patterns) =>
        new(patterns, OperatingSystem.IsWindows());

    [Test]
    public void ForScanRoot_RootWithDotDot_IsNormalisedBeforeTheAncestorCheck()
    {
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cdeexcl"));
        var target = Path.Combine(baseDir, "b");
        var m = ForHost(target).ForScanRoot(Path.Combine(baseDir, "a", "..", "b"));
        Assert.That(m.IsExcluded(Path.Combine(target, "x")), Is.False);
    }

    [Test]
    public void ForScanRoot_RelativeRoot_IsResolvedAgainstTheCurrentDirectory()
    {
        var cwd = Path.GetFullPath(".");
        var m = ForHost(cwd).ForScanRoot(".");
        Assert.That(m.IsExcluded(Path.Combine(cwd, "x")), Is.False);
    }

    [Test]
    public void ForScanRoot_RootWithTrailingSeparator_StillDropsTheMatchingPattern()
    {
        var dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cdeexcl", "t"));
        var m = ForHost(dir).ForScanRoot(dir + Path.DirectorySeparatorChar);
        Assert.That(m.IsExcluded(Path.Combine(dir, "x")), Is.False);
    }

    [Test]
    public void BareName_MatchesTheEntrysOwnNameAtAnyDepth()
    {
        var m = Linux("node_modules");
        Assert.That(m.IsExcluded("/src/node_modules"), Is.True);
        Assert.That(m.IsExcluded("/src/a/b/node_modules"), Is.True);
        Assert.That(m.IsExcluded("/src/a/node_modules/pkg/lib"), Is.False); // an excluded parent is never descended into: only the entry's own name is checked
        Assert.That(m.IsExcluded("/src/a/b"), Is.False);
    }

    [Test]
    public void BareName_MatchesWholeSegmentOnly()
    {
        var m = Linux(".git");
        Assert.That(m.IsExcluded("/src/.github"), Is.False);
        Assert.That(m.IsExcluded("/src/my.git"), Is.False);
        Assert.That(m.IsExcluded("/src/.git"), Is.True);
    }

    [Test]
    public void BareName_StarMatchesWithinTheName()
    {
        var m = Linux("*.tmp");
        Assert.That(m.IsExcluded("/src/a/build.tmp"), Is.True);
        Assert.That(m.IsExcluded("/src/a/build.tmpx"), Is.False);
    }

    [Test]
    public void BareName_CaseRulesFollowTheOs()
    {
        Assert.That(Linux("node_modules").IsExcluded("/src/Node_Modules"), Is.False);
        Assert.That(Windows("node_modules").IsExcluded(@"C:\src\Node_Modules"), Is.True);
    }

    [Test]
    public void BareName_Windows_SeparatorAgnostic()
    {
        Assert.That(Windows(".git").IsExcluded(@"C:\src\a/.git"), Is.True);
    }

    [Test]
    public void BareName_IsNotEmpty_AndCoexistsWithAbsolutePatterns()
    {
        var m = Linux("node_modules", "/tmp");
        Assert.That(m.IsEmpty, Is.False);
        Assert.That(Linux("node_modules").IsEmpty, Is.False);
        Assert.That(m.IsExcluded("/tmp/x"), Is.True);
        Assert.That(m.IsExcluded("/a/node_modules"), Is.True);
    }

    [Test]
    public void BareName_ScanRootWithThatName_IsNeverExcluded_ButItsDescendantsOfThatNameAre()
    {
        var m = Linux("node_modules").ForScanRoot("/work/node_modules");
        Assert.That(m.IsExcluded("/work/node_modules"), Is.False);
        Assert.That(m.IsExcluded("/work/node_modules/src"), Is.False);
        Assert.That(m.IsExcluded("/work/node_modules/src/node_modules"), Is.True);
    }

    [Test]
    public void BareName_AncestorOfRootWithThatName_DoesNotExcludeTheScan()
    {
        var m = Linux("node_modules").ForScanRoot("/work/node_modules/pkg/");
        Assert.That(m.IsExcluded("/work/node_modules/pkg/lib"), Is.False);
        Assert.That(m.IsExcluded("/work/node_modules/pkg/lib/node_modules"), Is.True);
    }

    [Test]
    public void BareName_PatternWithSeparatorInsideRelativePath_StillIgnored()
    {
        Assert.That(Linux("a/node_modules").IsExcluded("/x/a/node_modules"), Is.False);
    }

    [Test]
    public void TrailingStar_ExcludesChildrenButNotTheFolderItself()
    {
        var m = Linux("/tmp/*");
        Assert.That(m.IsExcluded("/tmp"), Is.False);
        Assert.That(m.IsExcluded("/tmp/a"), Is.True);
        Assert.That(m.IsExcluded("/tmp/a/b"), Is.True);
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

    private static ScanExcludeMatcher Create(string[] user, bool useDefaults, bool isWindows, bool isLinux, bool isMacOs = false) =>
        ScanExcludeMatcher.Create(user, useDefaults, isWindows, isLinux, isMacOs);

    [TestCase("/proc")]
    [TestCase("/sys/kernel")]
    [TestCase("/dev")]
    [TestCase("/run/user/1000")]
    [TestCase("/tmp")]
    [TestCase("/var/tmp")]
    [TestCase("/var/cache/apt")]
    [TestCase("/var/spool")]
    [TestCase("/var/spool/mail/alice")]
    [TestCase("/var/lock")]
    [TestCase("/var/lock/subsys")]
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
    [TestCase("/var/spoolx")]
    [TestCase("/var/locks")]
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
        Assert.That(matcher.IsExcluded("/var/spool"), Is.False);
        Assert.That(matcher.IsExcluded("/var/lock"), Is.False);
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
        Assert.That(matcher.IsExcluded("/var/spool"), Is.False);
        Assert.That(matcher.IsExcluded("/var/lock"), Is.False);
    }

    [Test]
    public void Create_OnMacOs_HasNoBuiltInPathsEvenWhenDefaultsOn()
    {
        var matcher = Create(["/data/skip"], useDefaults: true, isWindows: false, isLinux: false);

        Assert.That(matcher.IsExcluded("/tmp"), Is.False);
        Assert.That(matcher.IsExcluded("/var/spool"), Is.False);
        Assert.That(matcher.IsExcluded("/var/lock"), Is.False);
        Assert.That(matcher.IsExcluded("/data/skip"), Is.True);
    }

    [TestCase("/dev")]
    [TestCase("/dev/disk0")]
    [TestCase("/.Spotlight-V100")]
    [TestCase("/.fseventsd/x")]
    public void Create_OnMacOsWithDefaults_ExcludesBuiltInPaths(string path)
    {
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: false, isMacOs: true);

        Assert.That(matcher.IsExcluded(path), Is.True);
    }

    [TestCase("/tmp")]
    [TestCase("/proc")]
    [TestCase("/Users/alice")]
    [TestCase("/Volumes/Backup/.fseventsd")]
    public void Create_OnMacOsWithDefaults_DoesNotExcludeOtherPaths(string path)
    {
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: false, isMacOs: true);

        Assert.That(matcher.IsExcluded(path), Is.False);
    }

    [Test]
    public void Create_OnMacOsDefaultsOff_NoBuiltInPathsAndUserPathsApply()
    {
        var matcher = Create(["/data/skip"], useDefaults: false, isWindows: false, isLinux: false, isMacOs: true);

        Assert.That(matcher.IsExcluded("/dev"), Is.False);
        Assert.That(matcher.IsExcluded("/.Spotlight-V100"), Is.False);
        Assert.That(matcher.IsExcluded("/data/skip"), Is.True);
    }

    [Test]
    public void Create_OnLinuxAndWindows_HaveNoMacOsPaths()
    {
        Assert.That(Create([], true, isWindows: false, isLinux: true).IsExcluded("/.fseventsd"), Is.False);
        Assert.That(Create([], true, isWindows: true, isLinux: false).IsExcluded("/.fseventsd"), Is.False);
    }

    [TestCase("/tmp")]
    [TestCase("/home/*/.cache")]
    public void NeverMatchingPatterns_OnWindows_FlagsRootedUnixPaths(string pattern)
    {
        Assert.That(ScanExcludeMatcher.NeverMatchingPatterns([pattern], isWindows: true), Is.EqualTo(new[] { pattern }));
    }

    [TestCase(@"C:\temp")]
    [TestCase("c:/temp")]
    [TestCase(@"\\server\share")]
    public void NeverMatchingPatterns_OnLinuxAndMac_FlagsDriveAndUncPaths(string pattern)
    {
        Assert.That(ScanExcludeMatcher.NeverMatchingPatterns([pattern], isWindows: false), Is.EqualTo(new[] { pattern }));
    }

    [Test]
    public void NeverMatchingPatterns_MatchingOrBarePatterns_AreNotFlagged()
    {
        Assert.That(ScanExcludeMatcher.NeverMatchingPatterns(["/tmp", "node_modules", "  ", null], isWindows: false), Is.Empty);
        Assert.That(ScanExcludeMatcher.NeverMatchingPatterns([@"C:\temp", @"\\server\share", "node_modules", "//server/share"], isWindows: true), Is.Empty);
    }

    [Test]
    public void NeverMatchingPattern_DoesNotChangeScanResults()
    {
        var matcher = Windows("/tmp", @"C:\skip");

        Assert.That(matcher.IsExcluded(@"C:\skip\a"), Is.True);
        Assert.That(matcher.IsExcluded(@"C:\tmp"), Is.False);
    }

    [TestCase("/var/spool")]
    [TestCase("/var/lock")]
    public void Create_ScanningDefaultExcludedPathItself_StillWorks(string root)
    {
        var matcher = Create([], useDefaults: true, isWindows: false, isLinux: true).ForScanRoot(root);

        Assert.That(matcher.IsExcluded(root + "/a"), Is.False);
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
    public void ForScanConfig_ExtraPatterns_AddToConfiguredPatterns()
    {
        var scan = new cdeLib.Infrastructure.Config.ScanSection { ExcludePaths = ["/cfg"], UseDefaultExcludes = false };

        var matcher = ScanExcludeMatcher.ForScanConfig(scan, ["/cli"]);

        Assert.That(matcher.IsExcluded("/cfg/a"), Is.True);
        Assert.That(matcher.IsExcluded("/cli/a"), Is.True);
        Assert.That(matcher.IsExcluded("/other"), Is.False);
    }

    [Test]
    public void ForScanConfig_ExtraPatterns_WorkWithNullSection()
    {
        Assert.That(ScanExcludeMatcher.ForScanConfig(null, ["/cli"]).IsExcluded("/cli/a"), Is.True);
    }

    [Test]
    public void ScanOptions_ExcludeIsRepeatable()
    {
        var result = cde.CommandLine.CommandLineParserBuilder.Build().ParseArguments<cde.CommandLine.ScanOptions>(
            ["scan", "/data", "--exclude", "/a", "--exclude", "/b/*"]);

        Assert.That(result, Is.InstanceOf<CommandLine.Parsed<cde.CommandLine.ScanOptions>>());
        Assert.That(((CommandLine.Parsed<cde.CommandLine.ScanOptions>)result).Value.Exclude,
            Is.EqualTo(new[] { "/a", "/b/*" }));
    }

    [Test]
    public void ScanSection_UseDefaultExcludesDefaultsToTrue()
    {
        Assert.That(new cdeLib.Infrastructure.Config.ScanSection().UseDefaultExcludes, Is.True);
    }
}
