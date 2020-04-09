using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using cdeLibTest.TestHelpers;
using NSubstitute;
using NUnit.Framework;
using Serilog;
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
            foreach (var file in Directory.GetFiles(".", "*.cde"))
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
            data[dataSize - 1] = (byte)(data[dataSize - 1] ^ 0xFF);
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

        //THIS Test sometimes fails, flakey for what reason?
        [Test]
        public async Task CanFindDuplicates()
        {
            var duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var rootEntry = new RootEntry(_configuration);
            rootEntry.PopulateRoot(FileHelper.TestDir2);
            var rootEntries = new List<RootEntry> { rootEntry };
            await duplication.ApplyHash(rootEntries);
            // all 7 Files are partial hashed.
            Assert.That(duplication.DuplicationStatistics().PartialHashes, Is.EqualTo(7));
            // all 7 files are full hashed because every file appears to have a duplicate at partial hash size.
            Assert.That(duplication.DuplicationStatistics().FullHashes, Is.EqualTo(7));

            // need a new Duplication class() between ApplyHash and GetDupePairs for valid operation.
            duplication = new TestDuplication(_logger, _configuration, _applicationDiagnostics);
            var dupes = duplication.GetDupePairs(rootEntries);

            Assert.That(dupes[0].Value.Count, Is.EqualTo(2)); // testset1 has 2 dupes
            Assert.That(dupes[1].Value.Count, Is.EqualTo(3)); // testset3 has 3 dupes

            Console.WriteLine();
            Console.WriteLine($"Dumping sets of duplicates count {dupes.Count}");
            foreach (var dupe in dupes)
            {
                Console.WriteLine($"set of dupes to {dupe.Key.Path}");
                foreach (var entry in dupe.Value)
                {
                    Console.WriteLine($"dupe set file {entry.FullPath}");
                }
            }
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public async Task Can_Acquire_hash_From_File()
        {
            var fullFileName = Path.Combine(FileHelper.TestDir2, "testset2");
            var hash = await _hashHelper.GetHashResponseFromFile(fullFileName, null);
            Assert.IsNotNull(hash.Hash);
        }
        // ReSharper restore InconsistentNaming
    }
}