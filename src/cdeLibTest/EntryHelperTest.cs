using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace cdeLibTest;

// ReSharper disable InconsistentNaming
[TestFixture]
public class EntryHelperTest
{
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [SetUp]
    public void Setup()
    {
        _config.ProgressUpdateInterval.Returns(5000);
    }

    [Test]
    public void MakeFullPath_ShortPath_ReturnsCorrectPath()
    {
        var re = new RootEntry(_config) { Path = @"C:\" };
        var de = new DirEntry(true) { Path = "folder" };
        re.AddChild(de);
        re.SetInMemoryFields();

        var result = EntryHelper.MakeFullPath(re, de);

        result.ShouldBe(@"C:\folder");
    }

    [Test]
    public void MakeFullPath_LongPath_DoesNotThrow()
    {
        // Create a path longer than 512 characters to test the StringBuilder capacity issue
        var re = new RootEntry(_config) { Path = @"C:\" };

        ICommonEntry current = re;
        DirEntry lastDir = null;

        // Build a deep directory structure: C:\very_long_folder_name_01\very_long_folder_name_02\...
        // Each segment is 25 chars, so ~21 levels = 525+ characters
        for (int i = 0; i < 25; i++)
        {
            var dirName = $"very_long_folder_name_{i:D2}";
            var de = new DirEntry(true) { Path = dirName };
            current.AddChild(de);
            current = de;
            lastDir = de;
        }

        re.SetInMemoryFields();

        // This should not throw ArgumentOutOfRangeException
        string result = null;
        Should.NotThrow(() => result = EntryHelper.MakeFullPath(lastDir.ParentCommonEntry, lastDir));

        // Verify the path is constructed correctly
        result.ShouldNotBeNull();
        result.ShouldStartWith(@"C:\");
        result.Length.ShouldBeGreaterThan(512, "Path should be longer than 512 characters to test the fix");
        result.ShouldContain("very_long_folder_name_");
        result.ShouldEndWith("very_long_folder_name_24");
    }

    [Test]
    public void MakeFullPath_PathWithExactly512Characters_DoesNotThrow()
    {
        var re = new RootEntry(_config) { Path = @"C:\" };
        re.SetInMemoryFields();

        // Create a single directory entry with a name that makes the total path exactly 512 chars
        // C:\ = 3 chars, so we need 509 chars for the name
        var longName = new string('a', 509);
        var de = new DirEntry(true) { Path = longName };
        re.AddChild(de);
        re.SetInMemoryFields();

        string result = null;
        Should.NotThrow(() => result = EntryHelper.MakeFullPath(re, de));

        result.ShouldNotBeNull();
        result.Length.ShouldBe(512);
    }

    [Test]
    public void MakeFullPath_MultipleCallsWithVaryingLengths_DoesNotThrow()
    {
        // Test that the StringBuilder capacity management works across multiple calls
        var re = new RootEntry(_config) { Path = @"C:\" };

        var shortDir = new DirEntry(true) { Path = "short" };
        var longName = new string('x', 600);
        var longDir = new DirEntry(true) { Path = longName };

        re.AddChild(shortDir);
        re.AddChild(longDir);
        re.SetInMemoryFields();

        // First call with short path
        var shortResult = EntryHelper.MakeFullPath(re, shortDir);
        shortResult.ShouldBe(@"C:\short");

        // Second call with long path (>512 chars) - this previously failed
        string longResult = null;
        Should.NotThrow(() => longResult = EntryHelper.MakeFullPath(re, longDir));
        longResult.Length.ShouldBeGreaterThan(512);

        // Third call with short path again - verify StringBuilder still works
        var shortResult2 = EntryHelper.MakeFullPath(re, shortDir);
        shortResult2.ShouldBe(@"C:\short");
    }

    [Test]
    public void MakeFullPathPooled_IsAliasForMakeFullPath()
    {
        var re = new RootEntry(_config) { Path = @"C:\" };
        var de = new DirEntry(true) { Path = "test" };
        re.AddChild(de);
        re.SetInMemoryFields();

        var result1 = EntryHelper.MakeFullPath(re, de);
        var result2 = EntryHelper.MakeFullPathPooled(re, de);

        result1.ShouldBe(result2);
    }

    [Test]
    public void MakeFullPath_WithEmptyPath_HandlesGracefully()
    {
        var re = new RootEntry(_config) { Path = @"C:\" };
        re.SetInMemoryFields();

        var de = new DirEntry(true) { Path = null };
        // Note: DirEntry.Path setter converts null to empty string

        // MakeFullPath should handle empty path without crashing
        // Since Path property converts null to empty string, result will be just the root path
        var result = EntryHelper.MakeFullPath(re, de);

        result.ShouldBe(@"C:\");
    }
}
// ReSharper restore InconsistentNaming
