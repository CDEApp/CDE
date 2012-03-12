using System;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib;
using cdeLib.Infrastructure;
using NUnit.Framework;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace AlphaFSTest
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    public class AlphaFSToys
    // ReSharper restore InconsistentNaming
    {
        [Ignore] // i got it wrong at moment
        [Test]
        public void GetVolumePathNamesForVolume()
        {
            const string vol = @"C:\temp";
            var vp = Volume.GetVolumePathNamesForVolume(vol);
            foreach (var s in vp)
            {
                Console.WriteLine(" vp {0}", s);
            }
        }

        [Test]
        public void GetVolumeInformation()
        {
            const string vol = @"C:\";
            var vi = Volume.GetVolumeInformation(vol); // requires a root volume specifier it seems
            Console.WriteLine("vi.FileSystemName {0}", vi.FileSystemName);
            Console.WriteLine("vi.MaximumComponentLength {0}", vi.MaximumComponentLength);
            Console.WriteLine("vi.HasPersistentAccessControlLists {0}", vi.HasPersistentAccessControlLists);
            Console.WriteLine("vi.SerialNumber {0}", vi.SerialNumber);
            Console.WriteLine("vi.Name {0}", vi.Name);
        }

        [Test]
        public void GetDiskFreeSpace()
        {
            const string path = @"C:\temp";
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

                //// these exceptions are specific to robins machine.
                //if (!(lastPath == @"H:\" || lastPath == @"S:\"|| lastPath == @"R:\")) // avoid exception
                //{
                //    var volMounts = Volume.GetVolumeMountPoints(vol); // bombs on H: dvdrw... 
                //    foreach (var volMount in volMounts)
                //    {
                //        Console.WriteLine("Volume Mount {0}", volMount);
                //    }
                //}
            }
        }

        [Test]
        [Ignore("Test for Jason's comprehension")]
        public void GetDirectoryRootDiscovery_NonTest()
        {
            var re = new RootEntry();
            var fn = re.GetDirectoryRoot(@"\\lyon\j$\foo\bar");
            var fn2 = re.GetDirectoryRoot(@"C:\windows\foo\bar");
            Console.WriteLine(fn);
            Console.WriteLine(fn2);
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

        [Test]
        public void GetFilesWithExtension_AFileNotEndingInButContainingPatternIsReturend_NotSureWhy()
        {
            const string name1 = "G-SN750B_02_S13UJ1NQ221583.cde";
            const string name2 = "G-SN750B_02_S13UJ1NQ221583.cde-backup-with-hash";
            var f1 = File.Create(name1);
            var f2 = File.Create(name2);
            f1.Close();
            f2.Close();
            //var files = Directory.GetFiles(".", "*.cde", SearchOption.TopDirectoryOnly);
            var files = AlphaFSHelper.GetFilesWithExtension(".", "cde");

            foreach (var file in files)
            {
                Console.WriteLine("file {0}", file);
            }

            //System.Threading.Thread.Sleep(1000); // delay 1 second

            File.Delete(name1);
            File.Delete(name2);

            Assert.That(files.Length, Is.EqualTo(1), "Oops somehow we got a file not ending in \"cde\" in our result set.");
        }

        [Ignore("Example problem with alphaFS")]
        [Test]
        public void GetFullPath_BugInAlphaFS_GRRR()
        {
            // BUG in AlphaFS. Path.FullGetPath()
            // BUG in AlphaFS. Path.GetDirectoryRoot()
            // if G: is G:\Test then it returns G:
            // if G: is G:\ then it returns G:
            // Where as System.IO.Path
            // if G: is G:\Test then it returns G:\Test
            // if G: is G:\ then it returns G:\

            var originalDir = Directory.GetCurrentDirectory();
            Console.WriteLine("0 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());

            var alphaFP = Path.GetFullPath(@"C:");
            var ioFP = System.IO.Path.GetFullPath(@"C:");
            Console.WriteLine("0 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", alphaFP);
            Console.WriteLine("0 System.IO.Path.GetFullPath(@\"C:\") {0}", ioFP);

            Console.WriteLine("0 Alphaleonis.Win32.Filesystem.Directory.GetDirectoryRoot(@\"C:\") {0}", Directory.GetDirectoryRoot(@"C:"));
            Console.WriteLine("0 System.IO.Directory.GetDirectoryRoot(@\"C:\") {0}", System.IO.Directory.GetDirectoryRoot(@"C:"));
            Console.WriteLine();

            Directory.SetCurrentDirectory(@"C:\Windows\");
            Console.WriteLine("1 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("1 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("1 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();

            Directory.SetCurrentDirectory(@"C:\");
            Console.WriteLine("2 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("2 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("2 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();

            Directory.SetCurrentDirectory(originalDir);
            Console.WriteLine("3 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("3 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("3 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();

            Assert.Fail();
        }

        [Test]
        public void Directory_GetDirectories_OK()
        {
            Directory.SetCurrentDirectory("../../../../..");
            var dirs = Directory.GetDirectories(".");
            var m = dirs.Contains("bin");
            Console.WriteLine(string.Join(",", dirs));
            Assert.That(dirs.Contains(@".\bin"));
            Assert.That(dirs.Contains(@".\lib"));
            Assert.That(dirs.Contains(@".\src"));
        }
        // ReSharper restore InconsistentNaming
    }
}
