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
            _logger = new Logger();
            _configuration = MockRepository.GenerateMock<IConfiguration>();
            _configuration.Stub(x => x.ProgressUpdateInterval).Return(100);
            _configuration.Stub(x => x.HashFirstPassSize).Return(1024);
            _configuration.Stub(x => x.DegreesOfParallelism).Return(1);

            _applicationDiagnostics = MockRepository.GenerateMock<IApplicationDiagnostics>();

            var random = new Random();
            const int dataSize = 256*1024;

            //write 2 duplicate files.
            Directory.CreateDirectory(FolderName);

            var data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream($"{FolderName}\\testset1",FileMode.Create));
            WriteFile(data, new FileStream($"{FolderName}\\testset1dupe", FileMode.Create));

            //no dupe
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream($"{FolderName}\\testset2", FileMode.Create));
            // force 2nd file of testset2 to be different at last byte.
            data[dataSize - 1] = (Byte)(data[dataSize - 1] ^ 0xFF);
            WriteFile(data, new FileStream($"{FolderName}\\testset2NotDupe", FileMode.Create));

            //3 dupes
            data = new Byte[dataSize];
            random.NextBytes(data);
            WriteFile(data, new FileStream($"{FolderName}\\testset3", FileMode.Create));
            WriteFile(data, new FileStream($"{FolderName}\\testset3dupe1", FileMode.Create));
            WriteFile(data, new FileStream($"{FolderName}\\testset3dupe2", FileMode.Create));
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

        public class TestDuplication: Duplication
        {
            public TestDuplication(ILogger logger, IConfiguration configuration, IApplicationDiagnostics applicationDiagnostics) : base(logger, configuration, applicationDiagnostics)
            {
            }

            public DuplicationStatistics DuplicationStatistcs()
            {
                return _duplicationStatistics;
            }
        }

        [Test]
        public void CanFindDuplicates()
        {
            var duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var rootEntry = new RootEntry();
            rootEntry.PopulateRoot($"{FolderName}\\");
            var rootEntries = new List<RootEntry> {rootEntry};
            duplication.ApplyMd5Checksum(rootEntries);
            // all 7 Files are partial hashed.
            Assert.That(duplication.DuplicationStatistcs().PartialHashes, Is.EqualTo(7));
            // all 7 files are full hashed because every file appears to have a duplicate at partial hash size.
            Assert.That(duplication.DuplicationStatistcs().FullHashes, Is.EqualTo(7));
            
            // need a new Duplication class() between ApplyMd5Checksum and GetDupePairs for valid operation.
            duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var dupes = duplication.GetDupePairs(rootEntries);

            Assert.That(dupes[0].Value.Count, Is.EqualTo(2)); // testset1 has 2 dupes
            Assert.That(dupes[1].Value.Count, Is.EqualTo(3)); // testset3 has 3 dupes

            Console.WriteLine();
            Console.WriteLine("Dumping sets of duplicates count {0}", dupes.Count);
            foreach (var dupe in dupes)
            {
                Console.WriteLine("set of dupes to {0}", dupe.Key.Path);
                foreach (var adupe in dupe.Value)
                {
                    Console.WriteLine("dupe set file {0}", adupe.FullPath);
                }
            }
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void Can_Acquire_hash_From_File()
        {
            var hash = HashHelper.GetMD5HashFromFile($"{FolderName}\\testset2");
            Assert.IsNotNull(hash.Hash);
        }

        [Test]
        [Ignore("Reason")]
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