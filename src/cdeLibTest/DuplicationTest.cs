using System;
using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib;
using cdeLib.Infrastructure;
using NUnit.Framework;
using Rhino.Mocks;

namespace cdeLibTest
{
    [TestFixture]
    class DuplicationTest
    {
        private ILogger _logger;
        private IConfiguration _configuration;
        private IApplicationDiagnostics _applicationDiagnostics;

        [SetUp]
        public void Setup()
        {
            _logger = MockRepository.GenerateMock<ILogger>();
            _configuration = MockRepository.GenerateMock<IConfiguration>();
            _applicationDiagnostics = MockRepository.GenerateMock<IApplicationDiagnostics>();
        }

        // ReSharper disable InconsistentNaming
        [Test]
        public void GetSizePairs_HashIrrelevant_NullIsNotAHashValue_PartialNotAUniqueHashForSize_OK()
        {
            var re1  = new RootEntry { Path = @"C:\" };
            var de1  = new DirEntry { Path = @"de1",  Size = 10, IsPartialHash = false }; de1.SetHash(10);
            var de2  = new DirEntry { Path = @"de2",  Size = 10, IsPartialHash = false }; de2.SetHash(11);
            var de3  = new DirEntry { Path = @"de3",  Size = 11, IsPartialHash = false }; de3.SetHash(10);
            var de4  = new DirEntry { Path = @"de4",  Size = 11, IsPartialHash = false }; de4.SetHash(11);
            var de5  = new DirEntry { Path = @"de5",  Size = 11, IsPartialHash = false }; de5.SetHash(11);
            var de6  = new DirEntry { Path = @"de6",  Size = 11, IsPartialHash = false }; de6.SetHash(12);
            var de7  = new DirEntry { Path = @"de7",  Size = 11, IsPartialHash = true  }; de7.SetHash(12);
            var de8  = new DirEntry { Path = @"de8",  Size = 11, IsPartialHash = false }; de8.SetHash(12);
            var de9  = new DirEntry { Path = @"de9",  Size = 11, IsPartialHash = false };
            var de10 = new DirEntry { Path = @"de10", Size = 11, IsPartialHash = true  }; de10.SetHash(13);
            re1.Children.Add(de1);
            re1.Children.Add(de2);
            re1.Children.Add(de3);
            re1.Children.Add(de4);
            re1.Children.Add(de5);
            re1.Children.Add(de6);
            re1.Children.Add(de7);
            re1.Children.Add(de8);
            re1.Children.Add(de9);
            re1.Children.Add(de10);
            var roots = new List<RootEntry> { re1 };
            re1.SetInMemoryFields();

            var d = new Duplication(_logger,_configuration,_applicationDiagnostics);
            var sizePairDictionary = d.GetSizePairs(roots);

            Console.WriteLine("Number of Size Pairs {0}", sizePairDictionary.Count);
            Assert.That(sizePairDictionary.Count, Is.EqualTo(2));

            var sumOfUniqueHashesForEachSize = GetSumOfUniqueHashesForEachSize_ExcludePartialHash(sizePairDictionary);
            Console.WriteLine("Sum of total unique hashes (split on filesize to) {0}", sumOfUniqueHashesForEachSize);
            Assert.That(sumOfUniqueHashesForEachSize, Is.EqualTo(5));
        }

        [Test]
        public void GetDupePairs_DupeHashDoesNotMatchDiffSizeFilesOrPartialHash_OK()
        {
            var re1  = new RootEntry { Path = @"C:\" };
            var de1  = new DirEntry { Path = @"de1",  Size = 10, IsPartialHash = false }; de1.SetHash(10);
            var de2  = new DirEntry { Path = @"de2",  Size = 10, IsPartialHash = false }; de2.SetHash(11);
            var de3  = new DirEntry { Path = @"de3",  Size = 11, IsPartialHash = false }; de3.SetHash(10);
            var de4  = new DirEntry { Path = @"de4",  Size = 11, IsPartialHash = false }; de4.SetHash(11);
            var de5  = new DirEntry { Path = @"de5",  Size = 11, IsPartialHash = false }; de5.SetHash(11);
            var de6  = new DirEntry { Path = @"de6",  Size = 11, IsPartialHash = false }; de6.SetHash(12);
            var de7  = new DirEntry { Path = @"de7",  Size = 11, IsPartialHash = true  }; de7.SetHash(12);
            var de8  = new DirEntry { Path = @"de8",  Size = 11, IsPartialHash = false }; de8.SetHash(12);
            var de9  = new DirEntry { Path = @"de9",  Size = 11, IsPartialHash = false };
            var de10 = new DirEntry { Path = @"de10", Size = 11, IsPartialHash = true  }; de10.SetHash(13);
            re1.Children.Add(de1);
            re1.Children.Add(de2);
            re1.Children.Add(de3);
            re1.Children.Add(de4);
            re1.Children.Add(de5);
            re1.Children.Add(de6);
            re1.Children.Add(de7);
            re1.Children.Add(de8);
            re1.Children.Add(de9);
            re1.Children.Add(de10);
            var roots = new List<RootEntry> { re1 };

            var d = new Duplication(_logger, _configuration, _applicationDiagnostics);
            var dp = d.GetDupePairs(roots);
            var dp1 = dp.First();

            // ReSharper disable PossibleNullReferenceException
            var f1 = dp1.Value.FirstOrDefault(x => x.ChildDE == de4).ChildDE;
            Assert.That(f1, Is.EqualTo(de4));
            var f2 = dp1.Value.FirstOrDefault(x => x.ChildDE == de5).ChildDE;
            Assert.That(f2, Is.EqualTo(de5));
            // ReSharper restore PossibleNullReferenceException
        }

        [Test]
        public void TestHashingWorks()
        {
            var de5  = new DirEntry { Path = @"de5", Size = 11, IsPartialHash = false }; de5.SetHash(11);
            var de6  = new DirEntry { Path = @"de6", Size = 11, IsPartialHash = false }; de6.SetHash(12);
            var de7  = new DirEntry { Path = @"de7", Size = 11, IsPartialHash = true  }; de7.SetHash(12);
            var de8  = new DirEntry { Path = @"de8", Size = 11, IsPartialHash = false }; de8.SetHash(12);

            var ah5 = Hash16.EqualityComparer.StaticGetHashCode(de5.Hash);
            var ah6 = Hash16.EqualityComparer.StaticGetHashCode(de6.Hash);
            var ah7 = Hash16.EqualityComparer.StaticGetHashCode(de7.Hash);
            var ah8 = Hash16.EqualityComparer.StaticGetHashCode(de8.Hash);
            Console.WriteLine("de5.Hash {0}  de6.Hash {1} de7.Hash {2} de8.Hash {3}", ah5, ah6, ah7, ah8);

            var a5 = DirEntry.EqualityComparer.StaticGetHashCode(de5);
            var a6 = DirEntry.EqualityComparer.StaticGetHashCode(de6);
            var a7 = DirEntry.EqualityComparer.StaticGetHashCode(de7);
            var a8 = DirEntry.EqualityComparer.StaticGetHashCode(de8);
            Console.WriteLine("de5 {0}  de6 {1} de7 {2} de8 {3}", a5, a6, a7, a8);
        }

        [Test]
        public void GetSizePairs_CheckSanityOfDupeSizeCountandDupeFileCount_Exercise()
        {
            var originalDir = Directory.GetCurrentDirectory();
            Console.WriteLine("0 Directory.GetCurrentDirectory() {0}", Directory.GetCurrentDirectory());
            var rootEntries = RootEntry.LoadCurrentDirCache();

            if (rootEntries.Count == 0)
            {
                Console.WriteLine("No Catalogs found.");
                Assert.Fail();
            }
            foreach (var r in rootEntries)
            {
                Console.WriteLine("loaded {0}", r.DefaultFileName);
            }

            var d = new Duplication(_logger, _configuration, _applicationDiagnostics);
            var sizePairDictionary = d.GetSizePairs(rootEntries);

            Console.WriteLine("Number of Size Pairs {0}", sizePairDictionary.Count);
            //Assert.That(sizePairDictionary.Count, Is.EqualTo(15809));

            var sumOfUniqueHashesForEachSize = GetSumOfUniqueHashesForEachSize_ExcludePartialHash(sizePairDictionary);
            Console.WriteLine("Sum of total unique hashes (split on filesize to) {0}", sumOfUniqueHashesForEachSize);
            //Assert.That(sumOfUniqueHashesForEachSize, Is.EqualTo(73939));

            var dupePairEnum = d.GetDupePairs(rootEntries);

            Console.WriteLine("Number of Dupe Pairs {0}", dupePairEnum.Count);
        }

        private static long GetSumOfUniqueHashesForEachSize_ExcludePartialHash(IEnumerable<KeyValuePair<long, List<PairDirEntry>>> sizePairDictionary)
        {
            var sumOfUniqueHashesForEachSize = 0L;
            foreach (var list in sizePairDictionary)
            {
                var fdeListOfSize = list.Value;
                var seenHash = new Dictionary<Hash16, int>();
                foreach (var flatDe in fdeListOfSize)
                {
                    // var hash = flatDe.ChildDE.Hash;
                    if (flatDe.ChildDE.IsHashDone   // because this is run on SizeDupe list it can have null hashes.
                        && !flatDe.ChildDE.IsPartialHash)
                    {
                        if (!seenHash.ContainsKey(flatDe.ChildDE.Hash))
                        {
                            seenHash[flatDe.ChildDE.Hash] = 0;
                        }
                        else
                        {
                            ++seenHash[flatDe.ChildDE.Hash];
                        }
                    }
                }
                sumOfUniqueHashesForEachSize = sumOfUniqueHashesForEachSize + seenHash.Count;
            }
            return sumOfUniqueHashesForEachSize;
        }

        [Test]
        public void GetDupePairs_CheckAllDupeFilesHaveFullHash_OK()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();

            var d = new Duplication(_logger, _configuration, _applicationDiagnostics);
            var dupePairEnum = d.GetDupePairs(rootEntries);

            foreach (var dupe in dupePairEnum)
            {
                foreach (var de in dupe.Value)
                {
                    if (de.ChildDE.IsPartialHash)
                    {
                        Console.WriteLine("Trouble partial hash {0}", de.FullPath);
                        Assert.Fail();
                    }
                }
            }
        }

        // this one is useful to test drive ApplyMd5Checksum outside of the app.
        // I copy a .cde file into the compile folder for testing, but it works with none.
        [Test]
        public void ApplyMd5Checksum_CheckDupesAndCompleteFullHash_DoesItEnsureAllPartialDupesAreFullHashed_Exercise()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var d = new Duplication(_logger, _configuration, _applicationDiagnostics);

            d.ApplyMd5Checksum(rootEntries);
        }
        // ReSharper restore InconsistentNaming
    }
}
