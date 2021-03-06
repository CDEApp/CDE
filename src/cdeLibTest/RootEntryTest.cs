﻿using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class RootEntryTest
    {
        [Test]
        public void Constructor_Minimal_Creates()
        {
            var a = new RootEntry();

            Assert.That(a, Is.Not.Null);
        }

        [Test]
        public void Constuctor_GetTree_OK()
        {
            const string p = @"C:\temp";
            var re = new RootEntry();
            re.RecurseTree(p);

            Assert.That(re, Is.Not.Null);
            Assert.That(re.Children, Is.Not.Null);
            Assert.That(re.Children.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Constuctor_GetTreeWithMoreThanOneLevel_OK()
        {
            const string p = @"C:\temp";
            var re = new RootEntry();
            re.RecurseTree(p);

            Assert.That(re, Is.Not.Null);
            var found = re.Children.Any(x => x.Children != null && x.Children.Count > 0);
            Assert.That(found, Is.True, "One of entries does not have children.");
        }

        //[Test]
        //public void FindDir_LookForDir_InRoot()
        //{
        //    const string rootPath = @"C:\";
        //    var re = new RootEntry { Path = rootPath };

        //    var foundEntry = re.FindDir(rootPath, @"C:\Moo");

        //    Assert.That(foundEntry, Is.InstanceOf(typeof(RootEntry)));
        //}

        //[Test]
        //public void FindDir_NotExistinRoot_ReturnRE()
        //{
        //    const string rootPath = @"C:\";
        //    const string testPath = @"C:\Groo";
        //    var re = new RootEntry { Path = rootPath };

        //    var foundEntry = re.FindDir(rootPath, testPath);

        //    Assert.That(foundEntry, Is.InstanceOf(typeof(RootEntry)));
        //}

        [Test]
        public void GetDriverLetterHint_SimpleRootPath_ReturnsDriveLetter()
        {
            var re = new RootEntryTestStub();

            var hint = re.GetDriverLetterHint(@"C:\", @"C:\");
            
            Assert.That(hint, Is.EqualTo("C"));
        }

        [Test]
        public void GetDriverLetterHint_SimpleRootPathOddAsRootDifferent_ReturnsRootDriveLetter()
        {
            var re = new RootEntryTestStub();

            var hint = re.GetDriverLetterHint(@"C:\", @"D:\");

            Assert.That(hint, Is.EqualTo("D"));
        }

        [Test]
        public void GetDriverLetterHint_SimplePath_ReturnsRootLetter()
        {
            var re = new RootEntryTestStub();

            var hint = re.GetDriverLetterHint(@"C:\MyFolder", @"C:\");

            Assert.That(hint, Is.EqualTo("C"));
        }

        [Test]
        public void GetDriverLetterHint_UncPath_ReturnsUNC()
        {
            var re = new RootEntryTestStub(isUnc:true);

            var hint = re.GetDriverLetterHint(@"\\server\basepath\path", @"doest matter");

            Assert.That(hint, Is.EqualTo("UNC"));
        }


        [Test]
        public void GetDefaultFileName_SimpleRootPath_ReturnsExpectedStuff()
        {
            var re = new RootEntryTestStub();

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"C:\", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"C"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"C-VolName.cde"));
        }

        [Test]
        public void GetDefaultFileName_SimpleRootPath2_ReturnsExpectedStuff()
        {
            // ReSharper disable RedundantArgumentName
            var re = new RootEntryTestStub(root: @"D:\", volName: "OtherValue", fullPath:@"D:\");
            // ReSharper restore RedundantArgumentName

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"D:\", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"D"));
            Assert.That(volRoot, Is.EqualTo(@"D:\"));
            Assert.That(volName, Is.EqualTo(@"OtherValue"));
            Assert.That(fileName, Is.EqualTo(@"D-OtherValue.cde"));
        }

  
        [Test]
        public void GetDefaultFileName_SimplePath_ReturnsExpectedStuff()
        {
            var re = new RootEntryTestStub(fullPath: @"C:\MyTestFolder");

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"C:\MyTestFolder", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"C"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"C-VolName-C__MyTestFolder.cde"));
        }

        [Test]
        public void GetDefaultFileName_SimplePath2_ReturnsExpectedStuff()
        {
            var re = new RootEntryTestStub(fullPath: @"C:\MyTestFolder\Mine");

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"C:\MyTestFolder\Mine", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"C"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"C-VolName-C__MyTestFolder_Mine.cde"));
        }

        [Test]
        public void GetDefaultFileName_NonRootedPath_UsesFullPathToScanPath()
        {
            var re = new RootEntryTestStub(fullPath: @"C:\Stuff\MyTestFolder\Mine");
            string hint, volRoot, volName;

            var canonicalName = re.CanonicalPath(@"MyTestFolder\Mine");
            var fileName = re.GetDefaultFileName(canonicalName, out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"C"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"C-VolName-C__Stuff_MyTestFolder_Mine.cde"));
        }

        [Test]
        public void GetDefaultFileName_RootedPathByLeadingSlash_UsingFullPath()
        {
            var re = new RootEntryTestStub(fullPath: @"C:\MyTestFolder\Mine");
            string hint, volRoot, volName;

            var canonicalName = re.CanonicalPath(@"\MyTestFolder\Mine");
            var fileName = re.GetDefaultFileName(canonicalName, out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"C"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"C-VolName-C__MyTestFolder_Mine.cde"));
        }

        [Test]
        public void GetDefaultFileName_UNCPath_UsesFullPath()
        {
            var re = new RootEntryTestStub(isUnc:true, fullPath: @"\\myserver\myshare");
            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"\\myserver\myshare", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"UNC"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"UNC-myserver_myshare.cde"));
        }

        [Test]
        public void GetDefaultFileName_UNCPath2_UsesFullPath()
        {
            var re = new RootEntryTestStub(isUnc: true, fullPath: @"\\myserver\myshare\stuff");
            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"\\myserver\myshare\stuff", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"UNC"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"UNC-myserver_myshare_stuff.cde"));
        }

        [Test]
        public void CanonicalPath_DeviceRelativePath_OK()
        {
            var re = new RootEntryTestStub(isUnc: false, root: @"g:\", fullPath: @"g:\");
            const string testPath = @"g:";

            var result = re.CanonicalPath(testPath);

            Assert.That(result, Is.EqualTo(@"G:\"));
        }

        [Test]
        public void CanonicalPath_TrailingSlash_OK()
        {
            var re = new RootEntryTestStub(isUnc: false, root: @"c:\", fullPath: @"c:\Windows");
            const string testPath = @"c:\Windows\";

            var result = re.CanonicalPath(testPath);

            Assert.That(result, Is.EqualTo(@"C:\Windows"));
        }

        [Test]
        public void CanonicalPath_UNCTrailingSlash_OK()
        {
            var re = new RootEntryTestStub(isUnc: true, root: @"\\Friday\d$", fullPath: @"\\Friday\d$");
            const string testPath = @"\\Friday\d$\";

            var result = re.CanonicalPath(testPath);

            Assert.That(result, Is.EqualTo(@"\\Friday\d$\"));
        }

        [Test]
        public void CanonicalPath_UNCTrailingSlash2_OK()
        {
            var re = new RootEntryTestStub(isUnc: true, root: @"\\Friday\d$", fullPath: @"\\Friday\d$");
            const string testPath = @"\\Friday\d$";

            var result = re.CanonicalPath(testPath);

            Assert.That(result, Is.EqualTo(@"\\Friday\d$\"));
        }

        private class RootEntryTestStub : RootEntry
        {
            private readonly string _root;
            private readonly string _volName;
            private readonly bool _isUnc;
            private readonly bool _isPathRooted;
            private readonly string _fullPath;

            public RootEntryTestStub(
                string root=@"C:\",
                string volName="VolName", 
                bool isUnc=false, 
                bool isPathRooted=true,
                string fullPath=@"C:\")
            {
                _root = root;
                _volName = volName;
                _isUnc = isUnc;
                _isPathRooted = isPathRooted;
                _fullPath = fullPath;
            }

            public override string GetDirectoryRoot(string path)
            {
                return _root;
            }

            public override string GetVolumeName(string rootPath)
            {
                return _volName;
            }

            public override bool IsUnc(string path)
            {
                return _isUnc;
            }

            public override bool IsPathRooted(string path)
            {
                return _isPathRooted;
            }

            public override string GetFullPath(string path)
            {
                return _fullPath;
            }

        }

        [Test]
        public void SetFullPath_OnRootDirectory_SetsAllFullPaths()
        {
            var re = new RootEntry { Path = @"C:\" };
            var fe1 = new DirEntry { Path = "fe1" };
            var de2 = new DirEntry(true) { Path = "de2" };
            var fe3 = new DirEntry { Path = "fe3" };
            re.Children.Add(fe1);
            re.Children.Add(de2);
            de2.Children.Add(fe3);
            re.SetInMemoryFields();

            Assert.That(re.FullPath, Is.EqualTo(@"C:\"));
            Assert.That(fe1.FullPath, Is.Null);// Is.EqualTo(@"C:\fe1")); FullPath only set on directories to save memory.
            Assert.That(de2.FullPath, Is.EqualTo(@"C:\de2"));
            Assert.That(fe3.FullPath, Is.Null);//Is.EqualTo(@"C:\de2\fe3"));
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    public class RootEntryTest_SortAllChildrenByPath : RootEntryTestBase
    {
        DirEntry de2a;
        DirEntry de2b;
        DirEntry de2c;
        DirEntry de3a;
        DirEntry de4a;
        private RootEntry re;

        [SetUp]
        public void BeforeEveryTest()
        {
            re = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
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
}
