using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// Characterisation of what a scan produces for a real directory tree: names, sizes, local-time
/// modified stamps, flags, junction handling and access-denied handling.
/// </summary>
[TestFixture]
public class ScanPipelineScanTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cdescan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (var d in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
            {
                if (new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    Directory.Delete(d); // remove junction only, never its target
            }
        }
        catch { /* best effort */ }
        try { ResetAcl(); Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private void ResetAcl()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var d in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
        {
            var di = new DirectoryInfo(d);
            var sec = di.GetAccessControl();
            var me = WindowsIdentity.GetCurrent().User;
            sec.RemoveAccessRule(new FileSystemAccessRule(me, FileSystemRights.ListDirectory, AccessControlType.Deny));
            di.SetAccessControl(sec);
        }
    }

    private static void MakeFile(string path, int size, DateTime localWrite)
    {
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTime(path, localWrite);
    }

    private void BuildTree()
    {
        var t = new DateTime(2021, 7, 15, 13, 45, 30, DateTimeKind.Local);
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        Directory.CreateDirectory(Path.Combine(_root, "sub", "deep.dir"));
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        Directory.CreateDirectory(Path.Combine(_root, "café 日本"));
        MakeFile(Path.Combine(_root, "a.txt"), 10, t);
        MakeFile(Path.Combine(_root, "noext"), 0, t.AddDays(1));
        MakeFile(Path.Combine(_root, ".gitignore"), 3, t.AddDays(2));
        MakeFile(Path.Combine(_root, "x.tar.gz"), 77, t.AddDays(3));
        MakeFile(Path.Combine(_root, "sub", "a.txt"), 20, t.AddDays(5));
        MakeFile(Path.Combine(_root, "sub", "deep.dir", "b.TXT"), 30, t.AddDays(6));
        MakeFile(Path.Combine(_root, "café 日本", "naïve ü.md"), 40, t.AddDays(7));

        // Creating children bumps a directory's write time, and NTFS updates the copy held in the parent's
        // directory listing lazily, so a directory's own time and its listed time can differ for a moment.
        // Pin every directory's time once its children exist so nothing changes under the scan.
        Directory.SetLastWriteTime(Path.Combine(_root, "sub", "deep.dir"), t.AddDays(10));
        Directory.SetLastWriteTime(Path.Combine(_root, "sub"), t.AddDays(11));
        Directory.SetLastWriteTime(Path.Combine(_root, "empty"), t.AddDays(12));
        Directory.SetLastWriteTime(Path.Combine(_root, "café 日本"), t.AddDays(13));
    }

    private static List<(string Rel, DirEntry E)> Flatten(RootEntry re)
    {
        var res = new List<(string, DirEntry)>();
        void Walk(IList<DirEntry> kids, string prefix)
        {
            if (kids == null) return;
            foreach (var k in kids)
            {
                var rel = prefix + k.Path;
                res.Add((rel, k));
                if (k.IsDirectory) Walk(k.Children, rel + "/");
            }
        }
        Walk(re.Children, "");
        return res.OrderBy(x => x.Item1, StringComparer.Ordinal).ToList();
    }

    private static RootEntry Scan(string path, bool follow = false, ScanExcludeMatcher excludes = null)
    {
        var re = new RootEntry();
        re.PopulateRoot(path, follow, excludes: excludes);
        return re;
    }

    private static ScanExcludeMatcher Excluding(params string[] patterns) =>
        new(patterns, OperatingSystem.IsWindows());

    private void BuildExcludeTree()
    {
        Directory.CreateDirectory(Path.Combine(_root, "keep"));
        Directory.CreateDirectory(Path.Combine(_root, "skip", "inner"));
        Directory.CreateDirectory(Path.Combine(_root, "keep", "skip"));
        File.WriteAllText(Path.Combine(_root, "keep", "k.txt"), "k");
        File.WriteAllText(Path.Combine(_root, "keep", "skip", "deep.txt"), "d");
        File.WriteAllText(Path.Combine(_root, "r.txt"), "r");
        File.WriteAllText(Path.Combine(_root, "skip", "s.txt"), "s");
        File.WriteAllText(Path.Combine(_root, "skip", "inner", "i.txt"), "i");
    }

    [Test]
    public void Scan_TempTree_EntriesMatchFileSystemInfoOracle()
    {
        BuildTree();
        var re = Scan(_root);
        var flat = Flatten(re);

        // Independent oracle: the FileSystemInfo-based constructor that scan used historically, fed the
        // way the old scan fed it — from enumeration, i.e. the directory-listing data (not a fresh
        // per-item query, which reads the item's own record and can differ for directories).
        var infos = new DirectoryInfo(_root).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .ToDictionary(i => Path.GetRelativePath(_root, i.FullName).Replace('\\', '/'), StringComparer.Ordinal);
        var expected = infos.Keys.ToList();
        expected.Sort(StringComparer.Ordinal);
        Assert.That(flat.Select(f => f.Rel), Is.EqualTo(expected));

        foreach (var (rel, e) in flat)
        {
            var full = Path.Combine(_root, rel.Replace('/', '\\'));
            var fsi = infos[rel];
            var oracle = new DirEntry(fsi);
            Assert.That(e.Path, Is.EqualTo(oracle.Path), rel);
            // Directory sizes are rolled up from their files by SetSummaryFields.
            var expectedSize = fsi is FileInfo fi
                ? fi.Length
                : new DirectoryInfo(full).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            Assert.That(e.Size, Is.EqualTo(expectedSize), rel);
            Assert.That(e.ModifiedTicks, Is.EqualTo(oracle.ModifiedTicks), rel);
            Assert.That(e.BitFields, Is.EqualTo(oracle.BitFields), rel);
        }
    }

    [Test]
    public void Scan_ModifiedIsLocalTime()
    {
        BuildTree();
        var re = Scan(_root);
        var a = Flatten(re).Single(f => f.Rel == "a.txt").E;
        Assert.That(a.Modified, Is.EqualTo(new DateTime(2021, 7, 15, 13, 45, 30)));
        Assert.That(a.Modified.Ticks, Is.EqualTo(new DateTime(2021, 7, 15, 13, 45, 30, DateTimeKind.Local).Ticks));
    }

    [Test]
    public void Scan_NameAndExtensionSplit()
    {
        BuildTree();
        var flat = Flatten(Scan(_root)).ToDictionary(f => f.Rel, f => f.E);
        Assert.That(flat[".gitignore"].NamePart, Is.EqualTo(".gitignore"));
        Assert.That(flat[".gitignore"].ExtPart, Is.Null);
        Assert.That(flat["x.tar.gz"].NamePart, Is.EqualTo("x.tar"));
        Assert.That(flat["x.tar.gz"].ExtPart, Is.EqualTo(".gz"));
        Assert.That(flat["noext"].ExtPart, Is.Null);
    }

    [Test]
    public void Scan_RepeatedNamesShareOneInstance_AndNothingIsInterned()
    {
        var unique = "uq" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(_root, "d1"));
        Directory.CreateDirectory(Path.Combine(_root, "d2"));
        File.WriteAllText(Path.Combine(_root, "d1", unique + ".zzq"), "x");
        File.WriteAllText(Path.Combine(_root, "d2", unique + ".zzq"), "x");
        var flat = Flatten(Scan(_root)).Where(f => f.E.NamePart == unique).Select(f => f.E).ToList();
        Assert.That(flat, Has.Count.EqualTo(2));
        Assert.That(ReferenceEquals(flat[0].NamePart, flat[1].NamePart), Is.True, "name shared");
        Assert.That(ReferenceEquals(flat[0].ExtPart, flat[1].ExtPart), Is.True, "ext shared");
        Assert.That(string.IsInterned(unique), Is.Null, "scan must not use the process-wide intern pool");
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public void Scan_Junction_NotFollowedByDefault_FollowedWhenAsked()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("junctions are Windows-only");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "inside.txt"), "hi");
        var scanRoot = Path.Combine(_root, "scan");
        Directory.CreateDirectory(scanRoot);
        var link = Path.Combine(scanRoot, "link");
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        { CreateNoWindow = true, RedirectStandardOutput = true, UseShellExecute = false };
        using (var p = Process.Start(psi)) { p!.WaitForExit(); }
        if (!Directory.Exists(link)) Assert.Ignore("could not create junction");

        var def = Flatten(Scan(scanRoot));
        var l = def.Single(f => f.Rel == "link").E;
        Assert.That(l.IsReparsePoint, Is.True);
        Assert.That(l.IsDirectory, Is.True);
        Assert.That(def.Select(f => f.Rel), Does.Not.Contain("link/inside.txt"));

        var followed = Flatten(Scan(scanRoot, follow: true));
        Assert.That(followed.Select(f => f.Rel), Does.Contain("link/inside.txt"));
    }

    [Test]
    [SupportedOSPlatform("windows")]
    public void Scan_AccessDeniedDirectory_RecordedAndSkipped()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("ACLs are Windows-only");
        var denied = Path.Combine(_root, "denied");
        Directory.CreateDirectory(denied);
        File.WriteAllText(Path.Combine(denied, "secret.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "ok.txt"), "x");
        var di = new DirectoryInfo(denied);
        var sec = di.GetAccessControl();
        var me = WindowsIdentity.GetCurrent().User;
        sec.AddAccessRule(new FileSystemAccessRule(me, FileSystemRights.ListDirectory, AccessControlType.Deny));
        di.SetAccessControl(sec);
        try { Directory.GetFileSystemEntries(denied); Assert.Ignore("deny ACE not effective here"); }
        catch (UnauthorizedAccessException) { }

        var re = Scan(_root);
        var rels = Flatten(re).Select(f => f.Rel).ToList();
        Assert.That(rels, Does.Contain("denied"));
        Assert.That(rels, Does.Contain("ok.txt"));
        Assert.That(rels, Does.Not.Contain("denied/secret.txt"));
        Assert.That(re.PathsWithUnauthorisedExceptions, Is.EqualTo(new[] { denied }));
        Assert.That(re.AccessDeniedFolderCount, Is.EqualTo(1));
        Assert.That(re.ExcludedFolderCount, Is.Zero);
    }

    [Test]
    public void Scan_ExcludedFolders_AreCountedSeparatelyFromAccessDenied()
    {
        BuildExcludeTree();
        var re = Scan(_root, excludes: Excluding(Path.Combine(_root, "skip"), Path.Combine(_root, "keep", "skip")));

        Assert.That(re.ExcludedFolderCount, Is.EqualTo(2));
        Assert.That(re.AccessDeniedFolderCount, Is.Zero);
    }

    [Test]
    public void Scan_NothingSkipped_CountsAreZero()
    {
        BuildExcludeTree();
        var re = Scan(_root);

        Assert.That(re.ExcludedFolderCount, Is.Zero);
        Assert.That(re.AccessDeniedFolderCount, Is.Zero);
    }

    [Test]
    public void Scan_ExcludedFolder_RecordedAsEmptyEntryAndNotEnumerated()
    {
        BuildExcludeTree();
        var re = Scan(_root, excludes: Excluding(Path.Combine(_root, "skip")));
        var rels = Flatten(re).Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Contain("skip"));
        Assert.That(rels, Does.Not.Contain("skip/s.txt"));
        Assert.That(rels, Does.Not.Contain("skip/inner"));
        Assert.That(rels, Does.Contain("keep/k.txt"));
        Assert.That(rels, Does.Contain("keep/skip/deep.txt"), "same name at a different depth is not excluded");
        Assert.That(re.PathsWithUnauthorisedExceptions ?? new List<string>(), Is.Empty, "an excluded folder is not an error");
    }

    [Test]
    public void Scan_ExcludeWithStar_MatchesPerSegment()
    {
        BuildExcludeTree();
        var rels = Flatten(Scan(_root, excludes: Excluding(Path.Combine(_root, "*", "skip")))).Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Contain("keep/skip"));
        Assert.That(rels, Does.Not.Contain("keep/skip/deep.txt"));
        Assert.That(rels, Does.Contain("skip/s.txt"), "star matches one segment only");
    }

    [Test]
    public void Scan_ExcludeMatchingTheScanRoot_ScansTheWholeTree()
    {
        BuildExcludeTree();
        var rels = Flatten(Scan(_root, excludes: Excluding(_root))).Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Contain("r.txt"));
        Assert.That(rels, Does.Contain("keep/k.txt"));
        Assert.That(rels, Does.Contain("skip/s.txt"));
    }

    [Test]
    public void Scan_RootBelowExcludedPattern_StillScansButDeeperPatternApplies()
    {
        BuildExcludeTree();
        var rels = Flatten(Scan(Path.Combine(_root, "skip"),
            excludes: Excluding(_root, Path.Combine(_root, "skip", "inner")))).Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Contain("s.txt"));
        Assert.That(rels, Does.Contain("inner"));
        Assert.That(rels, Does.Not.Contain("inner/i.txt"));
    }

    [Test]
    public void Scan_ExcludeWithTrailingSlashAndBadEntries_StillExcludes()
    {
        BuildExcludeTree();
        var rels = Flatten(Scan(_root, excludes: Excluding("", "relative/skip", Path.Combine(_root, "skip") + Path.DirectorySeparatorChar)))
            .Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Not.Contain("skip/s.txt"));
        Assert.That(rels, Does.Contain("keep/k.txt"));
    }

    [Test]
    public void Scan_ExcludedJunction_NotFollowedEvenWhenFollowJunctionsIsOn()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("junctions are Windows-only");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "inside.txt"), "hi");
        var scanRoot = Path.Combine(_root, "scan");
        Directory.CreateDirectory(scanRoot);
        var link = Path.Combine(scanRoot, "link");
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        { CreateNoWindow = true, RedirectStandardOutput = true, UseShellExecute = false };
        using (var p = Process.Start(psi)) { p!.WaitForExit(); }
        if (!Directory.Exists(link)) Assert.Ignore("could not create junction");

        var rels = Flatten(Scan(scanRoot, follow: true, excludes: Excluding(link))).Select(f => f.Rel).ToList();

        Assert.That(rels, Does.Contain("link"));
        Assert.That(rels, Does.Not.Contain("link/inside.txt"));
    }
}
