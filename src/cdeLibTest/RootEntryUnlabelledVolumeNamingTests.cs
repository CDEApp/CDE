using System.IO;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using cdeLibTest.TestHelpers;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>Default catalog names for a Windows volume with no label (#251): no empty part between dashes.</summary>
[TestFixture]
[NonParallelizable]
public class RootEntryUnlabelledVolumeNamingTests
{
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private RootEntry Entry(string fullPath, string volumeLabel) =>
        new(_config, new MockFileSystemAdapter(fullPath: fullPath, volumeLabel: volumeLabel));

    [Test]
    public void An_unlabelled_volume_root_is_named_by_its_drive_letter_alone()
    {
        var name = Entry(@"C:\", "").GetDefaultFileName(@"C:\", out var hint, out _, out var legacy);

        Assert.That(name, Is.EqualTo("C.cde"));
        Assert.That(hint, Is.EqualTo("C"));
        Assert.That(legacy, Is.EqualTo("C-.cde"));
    }

    [Test]
    public void A_folder_on_an_unlabelled_volume_has_one_dash_after_the_drive_letter()
    {
        var name = Entry(@"C:\MyTestFolder", "").GetDefaultFileName(@"C:\MyTestFolder", out _, out _, out var legacy);

        Assert.That(name, Is.EqualTo("C-C__MyTestFolder.cde"));
        Assert.That(legacy, Is.EqualTo("C--C__MyTestFolder.cde"));
    }

    [Test]
    public void A_labelled_volume_keeps_its_name_and_has_no_older_name()
    {
        var root = Entry(@"C:\", "Sys").GetDefaultFileName(@"C:\", out _, out _, out var rootLegacy);
        var folder = Entry(@"C:\MyTestFolder", "Sys").GetDefaultFileName(@"C:\MyTestFolder", out _, out _, out var folderLegacy);

        Assert.That(root, Is.EqualTo("C-Sys.cde"));
        Assert.That(folder, Is.EqualTo("C-Sys-C__MyTestFolder.cde"));
        Assert.That(rootLegacy, Is.Null);
        Assert.That(folderLegacy, Is.Null);
    }

    [Test]
    public void A_scan_keeps_writing_to_a_catalog_saved_under_the_older_name()
    {
        var dir = Directory.CreateTempSubdirectory("cde-unlabelled").FullName;
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var re = Entry(@"C:\MyTestFolder", "");
            re.Path = @"C:\MyTestFolder";
            re.DefaultFileName = re.GetDefaultFileName(re.Path, out _, out _);

            Assert.That(re.ScanCatalogFileName(), Is.EqualTo("C-C__MyTestFolder.cdex"), "no catalog yet: the new name");

            File.WriteAllText(Path.Combine(dir, "C--C__MyTestFolder.cdex"), "");
            Assert.That(re.ScanCatalogFileName(), Is.EqualTo("C--C__MyTestFolder.cdex"), "only the older one exists");

            File.WriteAllText(Path.Combine(dir, "C-C__MyTestFolder.cdex"), "");
            Assert.That(re.ScanCatalogFileName(), Is.EqualTo("C-C__MyTestFolder.cdex"), "both exist: the new name wins");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(dir, true);
        }
    }
}
