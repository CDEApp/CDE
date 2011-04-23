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
            re.RecurseTree(p);

            Assert.That(re, Is.Not.Null);
            Assert.That(re.Children, Is.Not.Null);
            Assert.That(re.Children.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Constuctor_GetTreeWithMoreThanOneLevel_OK()
        {
            var p = @"C:\temp";
            var re = new RootEntry();
            re.RecurseTree(p);

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

    }
    // ReSharper restore InconsistentNaming
}
