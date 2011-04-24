using System;
using Alphaleonis.Win32.Filesystem;
using NUnit.Framework;

namespace AlphaFSTest
{
    [TestFixture]
    public class AlphaFSToys
    {
        [Ignore] // i got it wrong at moment
        [Test]
        public void GetVolumePathNamesForVolume()
        {
            var vol = @"C:\temp";
            var vp = Volume.GetVolumePathNamesForVolume(vol);
            foreach (var s in vp)
            {
                Console.WriteLine(" vp {0}", s);
            }
        }

        [Test]
        public void GetVolumeInformation()
        {
            var vol = @"C:\";
            var vi = Volume.GetVolumeInformation(vol); // requires a root voluem specifier it seems
            Console.WriteLine("vi.FileSystemName {0}", vi.FileSystemName);
            Console.WriteLine("vi.MaximumComponentLength {0}", vi.MaximumComponentLength);
            Console.WriteLine("vi.HasPersistentAccessControlLists {0}", vi.HasPersistentAccessControlLists);
            Console.WriteLine("vi.SerialNumber {0}", vi.SerialNumber);
            Console.WriteLine("vi.Name {0}", vi.Name);
        }

        [Test]
        public void GetDiskFreeSpace()
        {
            var path = @"C:\temp";
            var dsi = Volume.GetDiskFreeSpace(path);
            Console.WriteLine("FreeBytesAvailable {0}", dsi.FreeBytesAvailable);
            Console.WriteLine("TotalNumberOfBytes {0}", dsi.TotalNumberOfBytes);

            var p = Directory.GetDirectoryRoot(path);
            Console.WriteLine("GetDirectoryRoot {0} => {1}", path, p);
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void GetVolumes_GetVolumePathNamesForVolume()
        {
            var vols = Volume.GetVolumes();
            foreach (var vol in vols)
            {
                Console.WriteLine("Volume {0}", vol);

                var volPaths = Volume.GetVolumePathNamesForVolume(vol);
                var lastPath = "";
                foreach (var volPath in volPaths)
                {
                    Console.WriteLine("Volume Path {0}", volPath);
                    lastPath = volPath;
                }

                if (!(lastPath == @"H:\" || lastPath == @"S:\")) // avoid exception
                {
                    var volMounts = Volume.GetVolumeMountPoints(vol); // bombs on H: dvdrw... 
                    foreach (var volMount in volMounts)
                    {
                        Console.WriteLine("Volume Mount {0}", volMount);
                    }
                }
            }
        }

        [Test]
        public void IsRootedPath_ForRootedPath_ReturnsTrue()
        {
            var a = Path.IsPathRooted(@"C:\");

            Assert.That(a, Is.True);
        }

        [Test]
        public void IsRootedPath_ForRelativePath_ReturnsFalse()
        {
            var a = Path.IsPathRooted(@"MyPath");

            Assert.That(a, Is.False);
        }

        [Test]
        public void IsRootedPath_ForLeadingSlashRootedPath_ReturnsTrue()
        {
            var a = Path.IsPathRooted(@"\MyPath");

            Assert.That(a, Is.True);
        }

        [Test]
        public void IsRootedPath_UncPath_ReturnsTrue()
        {
            var a = Path.IsPathRooted(@"\\myserver\myshare");

            Assert.That(a, Is.True);
        }

        [Test]
        public void GetDirectoryRoot_WithRelativePath_ReturnsCurrentDirRoot()
        {
            var root = Directory.GetDirectoryRoot(@"\Windows");

            Assert.That(root, Is.EqualTo(@"D:\"));  // my dev drive is D:
        }

        [Test]
        public void GetDirectoryNameWithoutRoot_OnPathPrefixSlash()
        {
            var a = Path.GetDirectoryNameWithoutRoot(@"\Data");

            Assert.That(a, Is.EqualTo(@""));
        }

        [Test]
        public void GetDirectoryName_Test1()
        {
            var a = Path.GetDirectoryName(@"\Data");

            Assert.That(a, Is.EqualTo(@"\"));
        }

        [Test]
        public void GetFullPath_OfLeadingSlashPath()
        {
            var a = Path.GetFullPath(@"\Data");

            Assert.That(a, Is.EqualTo(@"D:\Data"));
        }

        [Test]
        public void GetFullPath_OfUncPath()
        {
            var a = Path.GetFullPath(@"\\Friday\cache");

            Assert.That(a, Is.EqualTo(@"\\Friday\cache\"));
        }
        // ReSharper restore InconsistentNaming
    }
}
