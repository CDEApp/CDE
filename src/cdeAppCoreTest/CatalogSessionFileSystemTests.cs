using System;
using System.IO;
using System.Linq;
using cdeAppCore.Dtos;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeAppCoreTest;

/// <summary>
/// A session over a catalog whose root is a real temp folder, so entries can be checked against the
/// live file system (<see cref="cdeAppCore.Session.CatalogSession.ExistsOnFileSystem"/>).
/// </summary>
[TestFixture]
public class CatalogSessionFileSystemTests
{
    private static readonly EntryRefDto Root = new(0, 0);

    private string _dir;
    private CatalogFixture _fixture;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-session-fs-test").FullName;
        File.WriteAllText(Path.Combine(_dir, "real.txt"), "x");
        Directory.CreateDirectory(Path.Combine(_dir, "realdir"));

        var tree = new RootEntry { Path = _dir, VolumeName = "V", DefaultFileName = "t.cde", ActualFileName = "t.cde" };
        tree.AddChild(new DirEntry(false) { Path = "real.txt", Size = 1 });
        tree.AddChild(new DirEntry(false) { Path = "ghost.txt", Size = 1 });
        tree.AddChild(new DirEntry(true) { Path = "realdir" });
        tree.AddChild(new DirEntry(true) { Path = "ghostdir" });
        tree.SetInMemoryFields();
        _fixture = new CatalogFixture(tree);
    }

    [TearDown]
    public void TearDown()
    {
        _fixture.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private EntryRefDto Child(string name)
        => _fixture.Session.GetChildren(Root).Single(c => c.Name == name).Ref;

    [TestCase("real.txt", true)]
    [TestCase("ghost.txt", false)]
    [TestCase("realdir", true)]
    [TestCase("ghostdir", false)]
    public void ExistsOnFileSystem_ReflectsTheLiveFileSystem(string name, bool expected)
    {
        Assert.That(_fixture.Session.ExistsOnFileSystem(Child(name)), Is.EqualTo(expected));
    }

    [Test]
    public void GetSource_ReturnsTheLoadedCatalog()
    {
        var source = _fixture.Session.GetSource(0);

        Assert.That(source.RootPath, Is.EqualTo(_dir));
    }

    [Test]
    public void GetSource_UnknownCatalog_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _fixture.Session.GetSource(5));
    }
}
