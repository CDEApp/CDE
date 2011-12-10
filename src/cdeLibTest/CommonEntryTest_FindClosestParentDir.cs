using System;
using Alphaleonis.Win32.Filesystem;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    //[TestFixture]
    //class CommonEntryTest_FindClosestParentDir
    //{
    //    readonly string pathSep = Path.DirectorySeparatorChar;

    //    [ExpectedException(typeof(ArgumentException), ExpectedMessage = "Argument relativePath must be non empty.")]
    //    [Test]
    //    public void FindClosestParentDir_WithEmptyString_ThrowsException()
    //    {
    //        var re = new CommonEntryTestStub();

    //        re.FindClosestParentDir(string.Empty);
    //    }

    //    [Test]
    //    public void FindClosestParentDir_NotExistinRoot_ReturnRE()
    //    {
    //        var re = new CommonEntryTestStub();
    //        var de = new DirEntry { Path = "Groo" };
    //        re.Children.Add(de);
    //        const string relativePath = "Groo2";

    //        var ce = re.FindClosestParentDir(relativePath);

    //        Assert.That(ce, Is.InstanceOf(typeof(CommonEntryTestStub)));
    //    }

    //    [Test]
    //    public void FindClosestParentDir_FirstLevelFromRoot_ReturnCorrectDE()
    //    {
    //        var re = new CommonEntryTestStub();
    //        var de = new DirEntry { Path = "Groo" };
    //        re.Children.Add(de);
    //        const string relativePath = "Groo";

    //        var ce = re.FindClosestParentDir(relativePath);

    //        Assert.That(ce, Is.InstanceOf(typeof(DirEntry)));
    //        var foundDE = (DirEntry)ce;
    //        Assert.That(foundDE.Path, Is.EqualTo("Groo"));
    //    }


    //    [Test]
    //    public void FindClosestParentDir_SecondLevelFromRoot_ReturnCorrectDE()
    //    {
    //        var re = new CommonEntryTestStub();
    //        var de = new DirEntry(true) { Path = "Groo" };
    //        var de2 = new DirEntry { Path = "Broo" };
    //        re.Children.Add(de);
    //        de.Children.Add(de2);
    //        var relativePath = "Groo" + pathSep + "Broo";

    //        var ce = re.FindClosestParentDir(relativePath);

    //        Assert.That(ce, Is.InstanceOf(typeof(DirEntry)));
    //        var foundDE = (DirEntry)ce;
    //        Assert.That(foundDE.Path, Is.EqualTo("Broo"));
    //    }

    //    [Test]
    //    public void FindClosestParentDir_SecondLevelNonExistFromRoot_ReturnCorrectDE()
    //    {
    //        var re = new CommonEntryTestStub();
    //        var de = new DirEntry(true) { Path = "Groo" };
    //        var de2 = new DirEntry { Path = "Broo" };
    //        re.Children.Add(de);
    //        de.Children.Add(de2);
    //        var relativePath = "Groo" + pathSep + "Broo2";

    //        var ce = re.FindClosestParentDir(relativePath);

    //        Assert.That(ce, Is.InstanceOf(typeof(DirEntry)));
    //        var foundDE = (DirEntry)ce;
    //        Assert.That(foundDE.Path, Is.EqualTo("Groo"));
    //    }
    //}
    // ReSharper restore InconsistentNaming
}
