using System;
using System.IO;
using System.Threading.Tasks;
using cdeAppCore;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NUnit.Framework;
using Serilog.Core;

namespace cdeAppCoreTest;

/// <summary>
/// Catalog discovery in <see cref="LoadCatalogService"/>: it searches the current directory and the requested
/// folder (one level down each), so the two can overlap. Changes the process current directory for the
/// duration of each test (this project does not run tests in parallel).
/// </summary>
[TestFixture]
public class LoadCatalogServiceTests
{
    private string _dir;
    private string _otherDir;
    private string _cwdBefore;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-load-catalog-test").FullName;
        _otherDir = Directory.CreateTempSubdirectory("cde-load-catalog-other").FullName;
        _cwdBefore = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_cwdBefore);
        Directory.Delete(_dir, recursive: true);
        Directory.Delete(_otherDir, recursive: true);
    }

    private RootEntry ScanData()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        var re = new RootEntry();
        re.PopulateRoot(data);
        return re;
    }

    private async Task WriteLegacyCatalogAsync()
    {
        var re = ScanData();
        re.ActualFileName = Path.Combine(_dir, "one.cde");
        using var repo = new CatalogRepository(Logger.None);
        await repo.Save(re);
    }

    private void WriteColumnarCatalog()
    {
        var re = ScanData();
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        ColumnarFormat.Write(re, Path.Combine(_dir, "one.cdex"));
    }

    // "." and the requested folder are the same directory, the default when the app runs in its catalog folder.
    [TestCase(".")]
    [TestCase("./")]
    [TestCase(null, TestName = "requested folder given as the absolute current directory")]
    public async Task Legacy_catalog_is_loaded_once_when_the_folders_overlap(string configPath)
    {
        await WriteLegacyCatalogAsync();

        var roots = await new LoadCatalogService(Logger.None)
            .LoadRootEntriesAsync(configPath ?? _dir, null);

        Assert.That(roots, Has.Count.EqualTo(1));
    }

    [TestCase(".")]
    [TestCase(null, TestName = "requested folder given as the absolute current directory")]
    public void Columnar_catalog_is_listed_once_when_the_folders_overlap(string configPath)
    {
        WriteColumnarCatalog();

        var files = new LoadCatalogService(Logger.None).GetColumnarFiles(configPath ?? _dir);

        Assert.That(files, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Without_the_current_directory_only_the_requested_folder_is_searched()
    {
        await WriteLegacyCatalogAsync();
        WriteColumnarCatalog();
        var service = new LoadCatalogService(Logger.None, includeCurrentDirectory: false);

        Assert.That(await service.LoadRootEntriesAsync(_otherDir, null), Is.Empty);
        Assert.That(service.GetColumnarFiles(_otherDir), Is.Empty);
    }

    [Test]
    public async Task By_default_the_current_directory_is_searched_too()
    {
        await WriteLegacyCatalogAsync();
        WriteColumnarCatalog();
        var service = new LoadCatalogService(Logger.None);

        Assert.That(await service.LoadRootEntriesAsync(_otherDir, null), Has.Count.EqualTo(1));
        Assert.That(service.GetColumnarFiles(_otherDir), Has.Count.EqualTo(1));
    }
}
