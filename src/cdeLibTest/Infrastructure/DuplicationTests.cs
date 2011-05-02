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
        public const string FolderName = "test";

        [TearDown]
        public void Teardown()
        {
            Directory.Delete(FolderName, true);
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
            Directory.CreateDirectory(FolderName);

            var data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream(String.Format("{0}\\testset1",FolderName),FileMode.Create));
            WriteFile(data, new FileStream(String.Format("{0}\\testset1dupe",FolderName), FileMode.Create));

            //no dupe
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream(String.Format("{0}\\testset2",FolderName), FileMode.Create));

            //3 dupes
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream(String.Format("{0}\\testset3",FolderName), FileMode.Create));
            WriteFile(data, new FileStream(String.Format("{0}\\testset3dupe1",FolderName), FileMode.Create));
            WriteFile(data, new FileStream(String.Format("{0}\\testset3dupe2", FolderName), FileMode.Create));
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
        public void CanFindDuplicates()
        {
            var duplication = new Duplication();
            var rootEntry = new RootEntry();
            rootEntry.PopulateRoot(String.Format("{0}\\",FolderName));
            var rootEntries = new List<RootEntry> {rootEntry};
            duplication.ApplyMd5Checksum(rootEntries);
            duplication.FindDuplicates(rootEntries);
            
            //Do Assertion on count of dupes, should be 2 collections.

        }
    }
}