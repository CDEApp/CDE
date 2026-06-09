using System.Linq;
using cdeAppCore.Dtos;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class CatalogSessionTests
{
    private CatalogFixture _fixture;
    private static readonly EntryRefDto Root = new(0, 0);

    [SetUp]
    public void SetUp() => _fixture = new CatalogFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public void Load_OpensOneCatalog()
    {
        Assert.That(_fixture.Session.CatalogCount, Is.EqualTo(1));
    }

    [Test]
    public void TotalEntryCount_CountsDirsAndFiles()
    {
        // 2 dirs (dir1, docs) + 4 files (alpha.txt, beta.log, alpha.md, root_file.txt)
        Assert.That(_fixture.Session.TotalEntryCount, Is.EqualTo(6));
    }

    [Test]
    public void GetCatalogs_ExposesMetadata()
    {
        var catalogs = _fixture.Session.GetCatalogs();
        Assert.That(catalogs, Has.Count.EqualTo(1));
        var c = catalogs[0];
        Assert.That(c.CatalogId, Is.EqualTo(0));
        Assert.That(c.RootPath, Is.EqualTo(@"C:\test"));
        Assert.That(c.VolumeName, Is.EqualTo("VOL"));
    }

    [Test]
    public void GetChildren_ReturnsAllImmediateChildren()
    {
        var children = _fixture.Session.GetChildren(Root);
        var names = children.Select(c => c.Name).OrderBy(n => n).ToArray();
        Assert.That(names, Is.EqualTo(new[] { "dir1", "docs", "root_file.txt" }));
    }

    [Test]
    public void GetChildren_FoldersOnly_ReturnsOnlyDirectories()
    {
        var folders = _fixture.Session.GetChildren(Root, foldersOnly: true);
        Assert.That(folders, Has.Count.EqualTo(2));
        Assert.That(folders.All(f => f.IsDirectory), Is.True);
        Assert.That(folders.Select(f => f.Name).OrderBy(n => n), Is.EqualTo(new[] { "dir1", "docs" }));
    }

    [Test]
    public void GetChildren_PagesWithSkipAndTake()
    {
        var all = _fixture.Session.GetChildren(Root);
        var page = _fixture.Session.GetChildren(Root, skip: 1, take: 1);
        Assert.That(page, Has.Count.EqualTo(1));
        Assert.That(page[0].Name, Is.EqualTo(all[1].Name));
    }

    [Test]
    public void GetPath_ReturnsRootToEntryChain()
    {
        var docs = _fixture.Session.GetChildren(Root, foldersOnly: true).Single(c => c.Name == "docs");
        var alphaMd = _fixture.Session.GetChildren(docs.Ref).Single(c => c.Name == "alpha.md");

        var path = _fixture.Session.GetPath(alphaMd.Ref);

        Assert.That(path.Select(p => p.Name), Is.EqualTo(new[] { @"C:\test", "docs", "alpha.md" }));
    }

    [Test]
    public void ResolveFullPath_BuildsFullPath()
    {
        var docs = _fixture.Session.GetChildren(Root, foldersOnly: true).Single(c => c.Name == "docs");
        var alphaMd = _fixture.Session.GetChildren(docs.Ref).Single(c => c.Name == "alpha.md");

        Assert.That(_fixture.Session.ResolveFullPath(alphaMd.Ref), Is.EqualTo(@"C:\test\docs\alpha.md"));
    }
}
