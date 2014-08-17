using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using cdeLib;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Hashing;
using NUnit.Framework;
using Rhino.Mocks;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileMode = System.IO.FileMode;

namespace cdeLibTest.Infrastructure
{
    public class DuplicationTests
    {
        private const string FolderName = "test";
        private ILogger _logger;
        private IConfiguration _configuration;
        private IApplicationDiagnostics _applicationDiagnostics;

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
            _logger = MockRepository.GenerateMock<ILogger>();
            _configuration = MockRepository.GenerateMock<IConfiguration>();
            _configuration.Stub(x => x.ProgressUpdateInterval).Return(100);
            _configuration.Stub(x => x.HashFirstPassSize).Return(128);
            _configuration.Stub(x => x.DegreesOfParallelism).Return(1);

            _applicationDiagnostics = MockRepository.GenerateMock<IApplicationDiagnostics>();

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
            var duplication = new Duplication(_logger, _configuration, _applicationDiagnostics);
            var rootEntry = new RootEntry();
            rootEntry.PopulateRoot(String.Format("{0}\\",FolderName));
            var rootEntries = new List<RootEntry> {rootEntry};
            duplication.ApplyMd5Checksum(rootEntries);
            duplication.FindDuplicates(rootEntries);
            
            //Do Assertion on count of dupes, should be 2 collections.

        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void Can_Acquire_hash_From_File()
        {
            var hashHelper = new HashHelper();
            var hash = HashHelper.GetMD5HashFromFile(String.Format("{0}\\testset2", FolderName));
            Assert.IsNotNull(hash.Hash);

        }

        [Test]
        [Ignore]
        public void Test_Memory_Usage_Of_MD5_Stream()
        {
            Process currentProcess = Process.GetCurrentProcess();
            long totalBytesOfMemoryUsed = currentProcess.WorkingSet64;
            Console.WriteLine("Memory Usage {0}",totalBytesOfMemoryUsed);
            var hashHelper = new HashHelper();
            var hash = HashHelper.GetMD5HashFromFile("C:\\temp\\a\\aaf-tomorrow.when.the.war.began.2010.720p.bluray.x264.mkv");
            // get the current process
            // get the physical mem usage
        }
        // ReSharper restore InconsistentNaming
    }
}