using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using cdeLib;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using cdeLibTest.TestHelpers;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Shouldly;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Infrastructure
{
    public class DuplicationTests
    {
        private ILogger _logger;
        private IConfiguration _configuration;
        private IApplicationDiagnostics _applicationDiagnostics;
        private HashHelper _hashHelper;

        [TearDown]
        public void Teardown()
        {
            Directory.Delete(FileHelper.TestDir2, true);
            var files = Directory.GetFiles(".", "*.cde");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _configuration = Substitute.For<IConfiguration>();
            _configuration.ProgressUpdateInterval.Returns(100);
            _configuration.HashFirstPassSize.Returns(1024);
            _configuration.DegreesOfParallelism.Returns(1);
            _configuration.Config.Returns(new AppConfigurationSection()
                { Display = new DisplaySection() { ConsoleLogToSeq = true } });

            var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            _logger = new Logger(_configuration, logger);
            _hashHelper = new HashHelper(_logger);

            

            _applicationDiagnostics = Substitute.For<IApplicationDiagnostics>();
            var random = new Random();
            const int dataSize = 256 * 1024;

            //write 2 duplicate files.
            Directory.CreateDirectory(FileHelper.TestDir2);

            var data = new byte[dataSize];
            // 2 dupes
            random.NextBytes(data);
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset1");
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset1dupe");

            // 0 dupes
            random.NextBytes(data);
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset2");
            // force 2nd file of testset2 to be different at last byte.
            data[dataSize - 1] = (byte) (data[dataSize - 1] ^ 0xFF);
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset2NotDupe");

            // 3 dupes
            data = new byte[dataSize];
            random.NextBytes(data);
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset3");
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset3dupe1");
            FileHelper.WriteFile(data, FileHelper.TestDir2, "testset3dupe2");
        }

        private class TestDuplication : Duplication
        {
            public TestDuplication(ILogger logger, IConfiguration configuration,
                IApplicationDiagnostics applicationDiagnostics) : base(logger, configuration, applicationDiagnostics)
            {
            }

            public DuplicationStatistics DuplicationStatistics()
            {
                return _duplicationStatistics;
            }
        }

        [Test]
        public void CanFindDuplicates()
        {
            var duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var rootEntry = new RootEntry(_configuration);
            rootEntry.PopulateRoot(FileHelper.TestDir2);
            var rootEntries = new List<RootEntry> {rootEntry};
            duplication.ApplyMd5Checksum(rootEntries);
            // all 7 Files are partial hashed.
            Assert.That(duplication.DuplicationStatistics().PartialHashes, Is.EqualTo(7));
            // all 7 files are full hashed because every file appears to have a duplicate at partial hash size.
            Assert.That(duplication.DuplicationStatistics().FullHashes, Is.EqualTo(7));

            // need a new Duplication class() between ApplyMd5Checksum and GetDupePairs for valid operation.
            duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var dupes = duplication.GetDupePairs(rootEntries);

            Assert.That(dupes[0].Value.Count, Is.EqualTo(2)); // testset1 has 2 dupes
            Assert.That(dupes[1].Value.Count, Is.EqualTo(3)); // testset3 has 3 dupes

            Console.WriteLine();
            Console.WriteLine($"Dumping sets of duplicates count {dupes.Count}");
            foreach (var dupe in dupes)
            {
                Console.WriteLine($"set of dupes to {dupe.Key.Path}");
                foreach (var adupe in dupe.Value)
                {
                    Console.WriteLine($"dupe set file {adupe.FullPath}");
                }
            }
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void Can_Acquire_hash_From_File()
        {
            var fullFileName = Path.Combine(FileHelper.TestDir2, "testset2");
            var hash = _hashHelper.GetMD5HashFromFile(fullFileName);
            Assert.IsNotNull(hash.Hash);
        }

        [Test]
        [Ignore("Reason")]
        public void Test_Memory_Usage_Of_MD5_Stream()
        {
            var currentProcess = Process.GetCurrentProcess();
            var totalBytesOfMemoryUsed = currentProcess.WorkingSet64;
            Console.WriteLine($"Memory Usage {totalBytesOfMemoryUsed}");
            var hash = _hashHelper.GetMD5HashFromFile(
                "C:\\temp\\a\\aaf-tomorrow.when.the.war.began.2010.720p.bluray.x264.mkv");

            // get the current process
            // get the physical mem usage
        }
        // ReSharper restore InconsistentNaming
    }
}