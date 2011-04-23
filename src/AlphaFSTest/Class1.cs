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
        // ReSharper restore InconsistentNaming

    }
}
