using System.IO;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using cdeLibTest.TestHelpers;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// Catalog names on Linux and macOS (#155): derived from the scan path, with no drive letter, volume label or
/// UNC part. The platform is given to <see cref="RootEntry"/> by its file system adapter, so these run on any host.
/// Windows names are covered by <see cref="RootEntryTest"/> and must not change.
/// </summary>
[TestFixture]
public class RootEntryNonWindowsNamingTests
{
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private (string FileName, string Hint, string VolumeRoot) Name(string scanPath)
    {
        var fs = new MockFileSystemAdapter(root: "/", fullPath: scanPath, isWindows: false);
        var fileName = new RootEntry(_config, fs).GetDefaultFileName(scanPath, out var hint, out var volumeRoot);
        return (fileName, hint, volumeRoot);
    }

    // The issue's table, one row each. GetDefaultFileName gives the legacy ".cde" name; the scan swaps it for ".cdex".
    [TestCase("/", "root")]
    [TestCase("/home", "home")]
    [TestCase("/mnt/data", "mnt_data")]
    [TestCase("/mnt/backups", "mnt_backups")]
    [TestCase("/mnt/nas/share", "mnt_nas_share")]
    [TestCase("/media/jason/USB1", "media_jason_USB1")]
    [TestCase("/var/log", "var_log")]
    public void The_name_comes_from_the_scan_path(string scanPath, string expected)
    {
        var (fileName, hint, _) = Name(scanPath);

        Assert.That(fileName, Is.EqualTo(expected + ".cde"));
        Assert.That(Path.ChangeExtension(fileName, ".cdex"), Is.EqualTo(expected + ".cdex"));
        Assert.That(hint, Is.EqualTo(expected), "the hint is the derived name, not a literal '/'");
    }

    [Test]
    public void There_is_no_drive_letter_volume_label_or_dash_prefix()
    {
        var (fileName, _, _) = Name("/mnt/data");

        Assert.That(fileName, Does.Not.Contain("-"));
        Assert.That(fileName, Does.Not.Contain("/"));
        Assert.That(fileName, Does.Not.StartWith("/"));
    }

    // Backslash, colon and dollar are replaced as on Windows, plus every other character that is invalid in a file name
    // there. The set is fixed, so the same path gives the same name on every host and the catalog is valid on any of them.
    [TestCase(@"/mnt/a\b", "mnt_a_b")]
    [TestCase("/mnt/a:b", "mnt_a_b")]
    [TestCase("/mnt/cost$", "mnt_cost_")]
    [TestCase("/mnt/a*b?c", "mnt_a_b_c")]
    [TestCase("/mnt/a\"b<c>d|e", "mnt_a_b_c_d_e")]
    [TestCase("/mnt/tab\there", "mnt_tab_here")]
    public void Characters_invalid_in_a_file_name_become_underscores(string scanPath, string expected)
    {
        Assert.That(Name(scanPath).FileName, Is.EqualTo(expected + ".cde"));
    }

    [Test]
    public void The_result_is_a_valid_file_name_whatever_the_path_holds()
    {
        var (fileName, _, _) = Name("/mnt/we ird: \\ $ * ? \" < > | \u0001 name");

        Assert.That(fileName.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1), fileName);
    }

    [Test]
    public void Spaces_and_other_characters_are_kept()
    {
        Assert.That(Name("/home/jason/My Documents").FileName, Is.EqualTo("home_jason_My Documents.cde"));
        Assert.That(Name("/mnt/caf\u00e9-data.v2").FileName, Is.EqualTo("mnt_caf\u00e9-data.v2.cde"));
    }

    [Test]
    public void Case_is_kept_so_paths_that_differ_only_in_case_do_not_collide()
    {
        var upper = Name("/mnt/Data").FileName;
        var lower = Name("/mnt/data").FileName;

        Assert.That(upper, Is.EqualTo("mnt_Data.cde"));
        Assert.That(lower, Is.EqualTo("mnt_data.cde"));
        Assert.That(upper, Is.Not.EqualTo(lower));
    }

    [Test]
    public void A_trailing_slash_does_not_change_the_name()
    {
        Assert.That(Name("/mnt/data/").FileName, Is.EqualTo(Name("/mnt/data").FileName));
        Assert.That(Name("/").FileName, Is.EqualTo("root.cde"));
    }

    // A known limitation, accepted in #155: the '/' to '_' rule cannot tell these two apart. It is rare, and a rescan of
    // the same path still replaces its own catalog.
    [Test]
    public void Known_limitation_an_underscore_and_a_slash_give_the_same_name()
    {
        Assert.That(Name("/mnt/data_x").FileName, Is.EqualTo(Name("/mnt/data/x").FileName));
    }

    [Test]
    public void The_volume_root_is_still_reported_by_the_adapter()
    {
        Assert.That(Name("/mnt/data").VolumeRoot, Is.EqualTo("/"));
    }

    [Test]
    public void A_windows_adapter_still_gives_the_windows_name_for_the_same_code_path()
    {
        var fs = new MockFileSystemAdapter(fullPath: @"C:\MyTestFolder\Mine", isWindows: true);

        var fileName = new RootEntry(_config, fs).GetDefaultFileName(@"C:\MyTestFolder\Mine", out var hint, out _);

        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(fileName, Does.EndWith("C__MyTestFolder_Mine.cde"));
    }
}
