using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class RootEntryTest
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
            var p = @"C:\temp";
            var re = new RootEntry();
            re.RecurseTree(p, null, null);

            Assert.That(re, Is.Not.Null);
            Assert.That(re.Children, Is.Not.Null);
            Assert.That(re.Children.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Constuctor_GetTreeWithMoreThanOneLevel_OK()
        {
            var p = @"C:\temp";
            var re = new RootEntry();
            re.RecurseTree(p, null, null);

            Assert.That(re, Is.Not.Null);
            var found = re.Children.Any(x => x.Children.Count > 0);
            Assert.That(found, Is.True, "One of entries does not have children.");
        }

        [Test]
        public void FindDir_LookForDir_InRoot()
        {
            const string rootPath = @"C:\";
            var re = new RootEntry { RootPath = rootPath };

            var foundEntry = re.FindDir(rootPath, @"C:\Moo");

            Assert.That(foundEntry, Is.InstanceOf(typeof(RootEntry)));
        }

        [Test]
        public void FindDir_NotExistinRoot_ReturnRE()
        {
            const string rootPath = @"C:\";
            const string testPath = @"C:\Groo";
            var re = new RootEntry { RootPath = rootPath };

            var foundEntry = re.FindDir(rootPath, testPath);

            Assert.That(foundEntry, Is.InstanceOf(typeof(RootEntry)));
        }

        [Test]
        public void GetDriverLetterHint_SimpleRootPath_ReturnsDriveLetter()
        {
            var re = new TestRootEntry();

            var hint = re.GetDriverLetterHint(@"C:\", @"C:\");
            
            Assert.That(hint, Is.EqualTo("C"));
        }

        [Test]
        public void GetDriverLetterHint_SimpleRootPathOddAsRootDifferent_ReturnsPath()
        {
            var re = new TestRootEntry();

            var hint = re.GetDriverLetterHint(@"C:\", @"D:\");

            Assert.That(hint, Is.EqualTo("PATH"));
        }

        [Test]
        public void GetDriverLetterHint_SimplePath_ReturnsPATH()
        {
            var re = new TestRootEntry();

            var hint = re.GetDriverLetterHint(@"C:\MyFolder", @"C:\");

            Assert.That(hint, Is.EqualTo("PATH"));
        }

        [Test]
        public void GetDriverLetterHint_UncPath_ReturnsUNC()
        {
            var re = new TestRootEntry(isUnc:true);

            var hint = re.GetDriverLetterHint(@"\\server\basepath\path", @"doest matter");

            Assert.That(hint, Is.EqualTo("UNC"));
        }


        [Test]
        public void GetDefaultFileName_SimpleRootPath_ReturnsExpectedStuff()
        {
            var re = new TestRootEntry();

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
            var re = new TestRootEntry(root:@"D:\", volName:"OtherValue", fullPath:@"D:\");

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
            var re = new TestRootEntry(fullPath: @"C:\MyTestFolder");

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"C:\MyTestFolder", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"PATH"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"PATH-C__MyTestFolder_.cde"));
        }

        [Test]
        public void GetDefaultFileName_SimplePath2_ReturnsExpectedStuff()
        {
            var re = new TestRootEntry(fullPath: @"C:\MyTestFolder\Mine");

            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"C:\MyTestFolder\Mine", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"PATH"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"PATH-C__MyTestFolder_Mine_.cde"));
        }

        [Test]
        public void GetDefaultFileName_NonRootedPath_UsesFullPathToScanPath()
        {
            var re = new TestRootEntry(fullPath: @"C:\Stuff\MyTestFolder\Mine");
            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"MyTestFolder\Mine", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"PATH"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"PATH-C__Stuff_MyTestFolder_Mine_.cde"));
        }

        [Test]
        public void GetDefaultFileName_RootedPathByLeadingSlash_UsingFullPath()
        {
            var re = new TestRootEntry(fullPath: @"C:\MyTestFolder\Mine");
            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"\MyTestFolder\Mine", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"PATH"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"PATH-C__MyTestFolder_Mine_.cde"));
        }

        [Test]
        public void GetDefaultFileName_UNCPath_UsesFullPath()
        {
            var re = new TestRootEntry(isUnc:true, fullPath: @"\\myserver\myshare");
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
            var re = new TestRootEntry(isUnc: true, fullPath: @"\\myserver\myshare\stuff");
            string hint, volRoot, volName;

            var fileName = re.GetDefaultFileName(@"\\myserver\myshare\stuff", out hint, out volRoot, out volName);

            Assert.That(hint, Is.EqualTo(@"UNC"));
            Assert.That(volRoot, Is.EqualTo(@"C:\"));
            Assert.That(volName, Is.EqualTo(@"VolName"));
            Assert.That(fileName, Is.EqualTo(@"UNC-myserver_myshare_stuff.cde"));
        }

        public class TestRootEntry : RootEntry
        {
            private readonly string _root;
            private readonly string _volName;
            private readonly bool _isUnc;
            private readonly bool _isPathRooted;
            private readonly string _fullPath;

            public TestRootEntry(
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

            public override string FullPath(string path)
            {
                return _fullPath;
            }

        }
    }
    // ReSharper restore InconsistentNaming
}
