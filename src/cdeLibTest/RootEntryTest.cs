using System.Linq;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using cdeLibTest.TestHelpers;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace cdeLibTest;

// ReSharper disable InconsistentNaming
[TestFixture]
public class RootEntryTest
{
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [SetUp]
    public void Setup()
    {
        _config.ProgressUpdateInterval.Returns(5000);
    }

    [Test]
    public void Constructor_Minimal_Creates()
    {
        var a = new RootEntry(_config);

        Assert.That(a, Is.Not.Null);
    }

    [Test]
    public void Constructor_GetTree_OK()
    {
        var re = new RootEntry(_config);
        re.RecurseTree(FileHelper.TestDir);

        Assert.That(re, Is.Not.Null);
        Assert.That(re.Children, Is.Not.Null);
        Assert.That(re.Children.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Constructor_GetTreeWithMoreThanOneLevel_OK()
    {
        var re = new RootEntry(_config);
        re.RecurseTree(FileHelper.TestDir);

        Assert.That(re, Is.Not.Null);
        var found = re.Children.Any(x => x.Children is { Count: > 0 });
        Assert.That(found, Is.True, "One of entries does not have children.");
    }

    [Test]
    public void GetDriverLetterHint_SimpleRootPath_ReturnsDriveLetter()
    {
        var mockFs = new MockFileSystemAdapter();
        var re = new RootEntry(_config, mockFs);

        var hint = re.GetDriverLetterHint(@"C:\", @"C:\");

        Assert.That(hint, Is.EqualTo("C"));
    }

    [Test]
    public void GetDriverLetterHint_SimpleRootPathOddAsRootDifferent_ReturnsRootDriveLetter()
    {
        var mockFs = new MockFileSystemAdapter();
        var re = new RootEntry(_config, mockFs);

        var hint = re.GetDriverLetterHint(@"C:\", @"D:\");

        Assert.That(hint, Is.EqualTo("D"));
    }

    [Test]
    public void GetDriverLetterHint_SimplePath_ReturnsRootLetter()
    {
        var mockFs = new MockFileSystemAdapter();
        var re = new RootEntry(_config, mockFs);

        var hint = re.GetDriverLetterHint(@"C:\MyFolder", @"C:\");

        Assert.That(hint, Is.EqualTo("C"));
    }

    [Test]
    public void GetDriverLetterHint_UncPath_ReturnsUNC()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: true);
        var re = new RootEntry(_config, mockFs);

        var hint = re.GetDriverLetterHint(@"\\server\basepath\path", "doest matter");

        Assert.That(hint, Is.EqualTo("UNC"));
    }

    [Test]
    public void GetDefaultFileName_SimpleRootPath_ReturnsExpectedStuff()
    {
        var mockFs = new MockFileSystemAdapter();
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"C:\", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        fileName.ShouldStartWith("C-");
    }

    [Test]
    public void GetDefaultFileName_SimpleRootPath2_ReturnsExpectedStuff()
    {
        var mockFs = new MockFileSystemAdapter(root: @"D:\", fullPath: @"D:\");
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"D:\", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("D"));
        Assert.That(volRoot, Is.EqualTo(@"D:\"));
        fileName.ShouldStartWith("D-");
    }

    [Test]
    public void GetDefaultFileName_SimplePath_ReturnsExpectedStuff()
    {
        var mockFs = new MockFileSystemAdapter(fullPath: @"C:\MyTestFolder");
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"C:\MyTestFolder", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        fileName.ShouldStartWith("C");
        fileName.ShouldEndWith("C__MyTestFolder.cde");
    }

    [Test]
    public void GetDefaultFileName_SimplePath2_ReturnsExpectedStuff()
    {
        var mockFs = new MockFileSystemAdapter(fullPath: @"C:\MyTestFolder\Mine");
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"C:\MyTestFolder\Mine", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        fileName.ShouldStartWith("C-");
        fileName.ShouldEndWith("C__MyTestFolder_Mine.cde");
    }

    [Test]
    public void GetDefaultFileName_NonRootedPath_UsesFullPathToScanPath()
    {
        var mockFs = new MockFileSystemAdapter(fullPath: @"C:\Stuff\MyTestFolder\Mine");
        var re = new RootEntry(_config, mockFs);

        var canonicalName = re.CanonicalPath(@"MyTestFolder\Mine");
        var fileName = re.GetDefaultFileName(canonicalName, out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        fileName.ShouldEndWith("C__Stuff_MyTestFolder_Mine.cde");
        fileName.ShouldStartWith("C");
    }

    [Test]
    public void GetDefaultFileName_RootedPathByLeadingSlash_UsingFullPath()
    {
        var mockFs = new MockFileSystemAdapter(fullPath: @"C:\MyTestFolder\Mine");
        var re = new RootEntry(_config, mockFs);

        var canonicalName = re.CanonicalPath(@"\MyTestFolder\Mine");
        var fileName = re.GetDefaultFileName(canonicalName, out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        Assert.That(fileName.Take(1), Is.EqualTo("C"));
        fileName.ShouldContain("C__MyTestFolder_Mine.cde");
    }

    [Test]
    public void GetDefaultFileName_UNCPath_UsesFullPath()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: true, fullPath: @"\\myserver\myshare");
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"\\myserver\myshare", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("UNC"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        Assert.That(fileName, Is.EqualTo("UNC-myserver_myshare.cde"));
    }

    [Test]
    public void GetDefaultFileName_UNCPath2_UsesFullPath()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: true, fullPath: @"\\myserver\myshare\stuff");
        var re = new RootEntry(_config, mockFs);

        var fileName = re.GetDefaultFileName(@"\\myserver\myshare\stuff", out var hint, out var volRoot);

        Assert.That(hint, Is.EqualTo("UNC"));
        Assert.That(volRoot, Is.EqualTo(@"C:\"));
        Assert.That(fileName, Is.EqualTo("UNC-myserver_myshare_stuff.cde"));
    }

    [Test]
    public void CanonicalPath_DeviceRelativePath_OK()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: false, root: @"g:\", fullPath: @"g:\");
        var re = new RootEntry(_config, mockFs);
        const string testPath = "g:";

        var result = re.CanonicalPath(testPath);

        Assert.That(result, Is.EqualTo(@"G:\"));
    }

    [Test]
    public void CanonicalPath_TrailingSlash_OK()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: false, root: @"c:\", fullPath: @"c:\Windows");
        var re = new RootEntry(_config, mockFs);
        const string testPath = @"c:\Windows\";

        var result = re.CanonicalPath(testPath);

        Assert.That(result, Is.EqualTo(@"C:\Windows"));
    }

    [Test]
    public void CanonicalPath_UNCTrailingSlash_OK()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: true, root: @"\\Friday\d$", fullPath: @"\\Friday\d$");
        var re = new RootEntry(_config, mockFs);
        const string testPath = @"\\Friday\d$\";

        var result = re.CanonicalPath(testPath);

        Assert.That(result, Is.EqualTo(@"\\Friday\d$\"));
    }

    [Test]
    public void CanonicalPath_UNCTrailingSlash2_OK()
    {
        var mockFs = new MockFileSystemAdapter(isUnc: true, root: @"\\Friday\d$", fullPath: @"\\Friday\d$");
        var re = new RootEntry(_config, mockFs);
        const string testPath = @"\\Friday\d$";

        var result = re.CanonicalPath(testPath);

        Assert.That(result, Is.EqualTo(@"\\Friday\d$\"));
    }

    [Test]
    public void SetFullPath_OnRootDirectory_SetsAllFullPaths()
    {
        var re = new RootEntry(_config) { Path = @"C:\" };
        var fe1 = new DirEntry { Path = "fe1" };
        var de2 = new DirEntry(true) { Path = "de2" };
        var fe3 = new DirEntry { Path = "fe3" };
        re.AddChild(fe1);
        re.AddChild(de2);
        de2.AddChild(fe3);
        re.SetInMemoryFields();

        Assert.That(re.FullPath, Is.EqualTo(@"C:\"));
        Assert.That(de2.FullPath, Is.EqualTo(@"C:\de2"));
    }
}
// ReSharper restore InconsistentNaming

// ReSharper disable InconsistentNaming
public class RootEntryTest_SortAllChildrenByPath : RootEntryTestBase
{
    private RootEntry re;
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [SetUp]
    public void BeforeEveryTest()
    {
        _config.ProgressUpdateInterval.Returns(5000);
        re = NewTestRootEntry(_config, out _, out _, out _, out _, out _);
    }

    [Test]
    public void SortAllChildrenByPaths_Sorts_Top_Level_InRoot()
    {
        re.SortAllChildrenByPath();

        Assert.That(re.Children[0].Path, Is.EqualTo("d2b")); //dirs first.
        Assert.That(re.Children[1].Path, Is.EqualTo("d2a"));
        Assert.That(re.Children[2].Path, Is.EqualTo("d2c"));
    }
}
// ReSharper restore InconsistentNaming