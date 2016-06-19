using System;
using System.IO;
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
        [Test]
        public void GetVolumeInformation()
        {
            const string vol = @"C:\";
            var vi = Volume.GetVolumeInfo(vol); // requires a root volume specifier it seems
            Console.WriteLine("vi.FileSystemName {0}", vi.FileSystemName);
            Console.WriteLine("vi.MaximumComponentLength {0}", vi.MaximumComponentLength);
            Console.WriteLine("vi.PersistentAcls {0}", vi.PersistentAcls);
            Console.WriteLine("vi.SerialNumber {0}", vi.SerialNumber);
            Console.WriteLine("vi.Name {0}", vi.Name);
        }

        [Test]
        public void GetDiskFreeSpace()
        {
            const string path = @"C:\temp";
            var dsi = Volume.GetDiskFreeSpace(path, false);
            Console.WriteLine("FreeBytesAvailable {0}", dsi.FreeBytesAvailable);
            Console.WriteLine("TotalNumberOfBytes {0}", dsi.TotalNumberOfBytes);

            var p = Directory.GetDirectoryRoot(path);
            Console.WriteLine("GetDirectoryRoot {0} => {1}", path, p);
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void GetVolumes_GetVolumePathNamesForVolume()
        {
            var vols = Volume.EnumerateVolumes();
            foreach (var vol in vols)
            {
                Console.WriteLine("Volume {0}", vol);
                var volPaths = Volume.EnumerateVolumePathNames(vol);
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
            var originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(@"C:\Windows\");
            var root = Directory.GetDirectoryRoot(@"\Windows");

            Assert.That(root, Is.EqualTo(@"C:\"));
            Directory.SetCurrentDirectory(originalDir);
        }

        [Test]
        public void GetDirectoryNameWithoutRoot_OnPathPrefixSlash()
        {
            var a = Path.GetDirectoryNameWithoutRoot(@"\Data");

            Assert.That(a, Is.EqualTo(null));
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
            var a = Path.GetFullPath(@"C:\Windows");

            Assert.That(a, Is.EqualTo(@"C:\Windows"));
        }

        [Test]
        public void GetFullPath_OfUncPath()
        {
            var a = Path.GetFullPath(@"\\Friday\cache");

            Assert.That(a, Is.EqualTo(@"\\Friday\cache"));
        }

        [Test]
        public void GetFullPath_OfUncPath_AddDirectorySeperator()
        {
            var a = Path.GetFullPath(@"\\Friday\cache");

            Assert.That(a, Is.EqualTo(@"\\?\UNC\Friday\cache\"));
        }

        [Test]
        public void GetFullPath_OfUncPath_LongPath()
        {
            var a = Path.GetFullPath(@"\\Friday\cache");

            Assert.That(a, Is.EqualTo(@"\\?\UNC\Friday\cache"));
        }

        [Test]
        public void GetFilesWithExtension_FileContainingPatternUseToReturn()
        {
            const string name1 = "G-SN750B_02_S13UJ1NQ221583.cde";
            const string name2 = "G-SN750B_02_S13UJ1NQ221583.cde-backup-with-hash";
            var f1 = File.Create(name1);
            var f2 = File.Create(name2);
            f1.Close();
            f2.Close();
            var files = AlphaFSHelper.GetFilesWithExtension(".", "cde");

            foreach (var file in files)
            {
                Console.WriteLine("file {0}", file);
            }

            //System.Threading.Thread.Sleep(1000); // delay 1 second

            File.Delete(name1);
            File.Delete(name2);

            Assert.That(files.Count(), Is.EqualTo(1), "Oops somehow we got a file not ending in \"cde\" in our result set.");
        }

        [Test]
        public void GetFullPath_GetDirectoryRoot_Behave_Like_SystemIO()
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
            Console.WriteLine();

            var alphaFP = Path.GetFullPath(@"C:");
            var ioFP = System.IO.Path.GetFullPath(@"C:");
            Console.WriteLine("0 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", alphaFP);
            Console.WriteLine("0 System.IO.Path.GetFullPath(@\"C:\") {0}", ioFP);
            Console.WriteLine();
            Assert.That(alphaFP, Is.EqualTo(ioFP));

            Console.WriteLine("0 Alphaleonis.Win32.Filesystem.Directory.GetDirectoryRoot(@\"C:\") {0}", Directory.GetDirectoryRoot(@"C:"));
            Console.WriteLine("0 System.IO.Directory.GetDirectoryRoot(@\"C:\") {0}", System.IO.Directory.GetDirectoryRoot(@"C:"));
            Console.WriteLine();
            Assert.That(Directory.GetDirectoryRoot(@"C:"), Is.EqualTo(System.IO.Directory.GetDirectoryRoot(@"C:")));

            Directory.SetCurrentDirectory(@"C:\Windows\");
            Console.WriteLine("1 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("1 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("1 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();
            Assert.That(Path.GetFullPath(@"C:"), Is.EqualTo(System.IO.Path.GetFullPath(@"C:")));

            Directory.SetCurrentDirectory(@"C:\");
            Console.WriteLine("2 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("2 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("2 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();
            Assert.That(Path.GetFullPath(@"C:"), Is.EqualTo(System.IO.Path.GetFullPath(@"C:")));

            Directory.SetCurrentDirectory(originalDir);
            Console.WriteLine("3 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            Console.WriteLine("3 Alphaleonis.Win32.Filesystem.Path.GetFullPath(@\"C:\") {0}", Path.GetFullPath(@"C:"));
            Console.WriteLine("3 System.IO.Path.GetFullPath(@\"C:\") {0}", System.IO.Path.GetFullPath(@"C:"));
            Console.WriteLine();
            Assert.That(Path.GetFullPath(@"C:"), Is.EqualTo(System.IO.Path.GetFullPath(@"C:")));
        }

        [Test]
        public void Directory_GetDirectories_OK()
        {
            Directory.SetCurrentDirectory(@"../..");
            var dirs = Directory.GetDirectories(".");
            var m = dirs.Contains("bin");
            Console.WriteLine(string.Join(",", dirs));
            Assert.That(dirs.Any(x => x.EndsWith(@"\obj")));
            Assert.That(dirs.Any(x => x.EndsWith(@"\bin")));
        }

        [Test]
        public void Directory_EnumerateDirectories_OK()
        {
            Directory.SetCurrentDirectory(@"../..");
            var dirs = Directory.EnumerateDirectories(".").ToArray();
            var m = dirs.Contains("bin");
            Console.WriteLine(string.Join(",", dirs));
            Assert.That(dirs.Any(x => x.EndsWith(@"\bin")));
            Assert.That(dirs.Any(x => x.EndsWith(@"\obj")));
        }

        [Test]
        public void Directory_EnumerateFiles_OK()
        {
            Directory.SetCurrentDirectory(@".");
            var dirs = Directory.EnumerateFiles(".").ToArray();
            var m = dirs.Contains("bin");
            Console.WriteLine(string.Join(",", dirs));
            //Assert.That(dirs.Contains(@".\bin"));  // NOW get full paths ? hmm.
            //Assert.That(dirs.Contains(@".\lib"));
            //Assert.That(dirs.Contains(@".\src"));
            Assert.That(dirs.Any(x => x.EndsWith(@"AlphaFSTest.pdb")));
            Assert.That(dirs.Any(x => x.EndsWith(@"AlphaFSTest.dll")));
        }


        //[Test]
        //public void Directory_GetFileSystemEntries_OK()
        //{
        //    Directory.SetCurrentDirectory("../../../..");
        //    var fileSystemEntries = Directory.GetFileSystemEntries(".");
        //    var m = fileSystemEntries.Contains("bin");
        //    Console.WriteLine(string.Join(",", fileSystemEntries));
        //    Assert.That(fileSystemEntries.Contains(@".\bin"));  // NOW get full paths ? hmm.
        //    Assert.That(fileSystemEntries.Contains(@".\lib"));
        //    Assert.That(fileSystemEntries.Contains(@".\src"));
        //}
        // ReSharper restore InconsistentNaming
    }
}
