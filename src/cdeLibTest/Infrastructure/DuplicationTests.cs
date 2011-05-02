using System;
using System.Collections.Generic;
using System.IO;
using cdeLib;
using NUnit.Framework;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileMode = System.IO.FileMode;

namespace cdeLibTest.Infrastructure
{
    public class DuplicationTests
    {
        [TearDown]
        public void Teardown()
        {
            Directory.Delete("test",true);
            var files = Directory.GetFiles(".\\", "*.cde");
            foreach (var file in files)
            {
                Alphaleonis.Win32.Filesystem.File.Delete(file);
            }

        }

        [SetUp]
        public void SetUp()
        {
            var random = new Random();
            const int dataSize = 256*1024;

            //write 2 duplicate files.
            Directory.CreateDirectory("test");

            var data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream("test\\testset1",FileMode.Create));
            WriteFile(data, new FileStream("test\\testset1dupe", FileMode.Create));

            //no dupe
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream("test\\testset2", FileMode.Create));

            //3 dupes
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream("test\\testset3", FileMode.Create));
            WriteFile(data, new FileStream("test\\testset3dupe1", FileMode.Create));
            WriteFile(data, new FileStream("test\\testset3dupe2", FileMode.Create));
        }

        private static void WriteFile(byte[] data, Stream fs)
        {
            BinaryWriter bw;
            using (bw = new BinaryWriter(fs))
            {
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
        }

        [Test]
        public void CanDetectDuplicates()
        {
            var duplication = new Duplication();
            var rootEntry = new RootEntry();
            rootEntry.PopulateRoot("test\\");
            var rootEntries = new List<RootEntry> {rootEntry};
            duplication.ApplyMd5Checksum(rootEntries);
            duplication.FindDuplicates(rootEntries);
            
            //Do Assertion on count of dupes, should be 2 collections.

        }
    }
}