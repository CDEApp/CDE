using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class StringExtensionTest
    {
        [Test]
        public void GetRelativePath_RootPathIsDriveRoot_WithTrailSeperator_ReturnPath()
        {
            const string rootPath = @"C:\";
            const string fullPath = @"C:\Moo";

            var relativePath = fullPath.GetRelativePath(rootPath);

            Assert.That(relativePath, Is.EqualTo("Moo"));
        }

        [Test]
        public void GetRelativePath_RootPathIsDriveRoot_WithoutTrailSeperator_ReturnPath()
        {
            const string rootPath = @"C:";
            const string fullPath = @"C:\Moo";

            var relativePath = fullPath.GetRelativePath(rootPath);

            Assert.That(relativePath, Is.EqualTo("Moo"));
        }

        [Test]
        public void GetRelativePath_RootIsDifferentTree_ToFullPath_ReturnPath()
        {
            const string rootPath = @"C:\Stuff";
            const string fullPath = @"C:\Moo";

            var relativePath = fullPath.GetRelativePath(rootPath);

            Assert.That(relativePath, Is.Null);
        }

        [Test]
        public void GetRelativePath_FullPathUnderRootPath_ReturnPath()
        {
            const string rootPath = @"C:\Moo";
            const string fullPath = @"C:\Moo\Stuff\Here";

            var relativePath = fullPath.GetRelativePath(rootPath);

            Assert.That(relativePath, Is.EqualTo(@"Stuff\Here"));
        }

        [Test]
        public void RemovePrefix_TestSameAsPrefix_ReturnEmptyString()
        {
            const string prefix = @"C:\Mooboy\Now";

            var rest = prefix.GetRelativePath(prefix);

            Console.WriteLine($"rest \"{rest}\"");
            Assert.That(rest, Is.EqualTo(string.Empty));
        }
    }
    // ReSharper restore InconsistentNaming
}
