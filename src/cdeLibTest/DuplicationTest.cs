using System;
using System.Collections.Generic;
using System.Linq;
using cdeLib;
using cdeLib.Infrastructure.Comparer;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    class DuplicationTest
    {
        // ReSharper disable InconsistentNaming
        [Test]
        public void GetSizePairs_HashIrrelevant_NullIsNotAHashValue_PartialNotAUniqueHashForSize_OK()
        {
            var re1  = new RootEntry { RootPath = @"C:\" };
            var de1  = new DirEntry { Name = @"de1",  Size = 10, Hash = new byte[] { 10 }, IsPartialHash = false };
            var de2  = new DirEntry { Name = @"de2",  Size = 10, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de3  = new DirEntry { Name = @"de3",  Size = 11, Hash = new byte[] { 10 }, IsPartialHash = false };
            var de4  = new DirEntry { Name = @"de4",  Size = 11, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de5  = new DirEntry { Name = @"de5",  Size = 11, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de6  = new DirEntry { Name = @"de6",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = false };
            var de7  = new DirEntry { Name = @"de7",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = true };
            var de8  = new DirEntry { Name = @"de8",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = false };
            var de9  = new DirEntry { Name = @"de9",  Size = 11, Hash = null, IsPartialHash = false };
            var de10 = new DirEntry { Name = @"de10", Size = 11, Hash = new byte[] { 13 }, IsPartialHash = true };
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

            var d = new Duplication();
            var sizePairDictionary = d.GetSizePairs(roots);

            Console.WriteLine("Number of Size Pairs {0}", sizePairDictionary.Count);
            Assert.That(sizePairDictionary.Count, Is.EqualTo(2));

            var sumOfUniqueHashesForEachSize = GetSumOfUniqueHashesForEachSize(sizePairDictionary);
            Console.WriteLine("Sum of total unique hashes (split on filesize to) {0}", sumOfUniqueHashesForEachSize);
            Assert.That(sumOfUniqueHashesForEachSize, Is.EqualTo(5));
        }

        [Test]
        public void GetDupePairs_DupeHashDoesNotMatchDiffSizeFilesOrPartialHash_OK()
        {
            var re1  = new RootEntry { RootPath = @"C:\" };
            var de1  = new DirEntry { Name = @"de1",  Size = 10, Hash = new byte[] { 10 }, IsPartialHash = false };
            var de2  = new DirEntry { Name = @"de2",  Size = 10, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de3  = new DirEntry { Name = @"de3",  Size = 11, Hash = new byte[] { 10 }, IsPartialHash = false };
            var de4  = new DirEntry { Name = @"de4",  Size = 11, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de5  = new DirEntry { Name = @"de5",  Size = 11, Hash = new byte[] { 11 }, IsPartialHash = false };
            var de6  = new DirEntry { Name = @"de6",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = false };
            var de7  = new DirEntry { Name = @"de7",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = true };
            var de8  = new DirEntry { Name = @"de8",  Size = 11, Hash = new byte[] { 12 }, IsPartialHash = false };
            var de9  = new DirEntry { Name = @"de9",  Size = 11, Hash = null, IsPartialHash = false };
            var de10 = new DirEntry { Name = @"de10", Size = 11, Hash = new byte[] { 13 }, IsPartialHash = true };
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

            var d = new Duplication();
            var dp = d.GetDupePairs(roots);
            var dp1 = dp.First();

            var f1 = dp1.Value.FirstOrDefault(x => x.DirEntry == de4).DirEntry;
            Assert.That(f1, Is.EqualTo(de4));
            var f2 = dp1.Value.FirstOrDefault(x => x.DirEntry == de5).DirEntry;
            Assert.That(f2, Is.EqualTo(de5));
        }

        [Test]
        public void GetSizePairs_CheckSanityOfDupeSizeCountandDupeFileCount_Exercise()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();

            var d = new Duplication();
            var sizePairDictionary = d.GetSizePairs(rootEntries);

            Console.WriteLine("Number of Size Pairs {0}", sizePairDictionary.Count);
            //Assert.That(sizePairDictionary.Count, Is.EqualTo(15809));

            var sumOfUniqueHashesForEachSize = GetSumOfUniqueHashesForEachSize(sizePairDictionary);
            Console.WriteLine("Sum of total unique hashes (split on filesize to) {0}", sumOfUniqueHashesForEachSize);
            //Assert.That(sumOfUniqueHashesForEachSize, Is.EqualTo(73939));

            var dupePairEnum = d.GetDupePairs(rootEntries);

            Console.WriteLine("Number of Dupe Pairs {0}", dupePairEnum.Count);
        }

        public static ulong GetSumOfUniqueHashesForEachSize(IEnumerable<KeyValuePair<ulong, List<FlatDirEntryDTO>>> sizePairDictionary)
        {
            var sumOfUniqueHashesForEachSize = 0ul;
            foreach (var list in sizePairDictionary)
            {
                var fdeListOfSize = list.Value;
                var seenHash = new Dictionary<byte[], int>(new ByteArrayComparer());
                foreach (var flatDe in fdeListOfSize)
                {
                    var hash = flatDe.DirEntry.Hash;
                    if (hash != null && !flatDe.DirEntry.IsPartialHash)   // because this is run on SizeDupe list it can have null hashes.
                    {
                        if (!seenHash.ContainsKey(hash))
                        {
                            seenHash[hash] = 0;
                        }
                        else
                        {
                            ++seenHash[hash];
                        }
                    }
                }
                sumOfUniqueHashesForEachSize = sumOfUniqueHashesForEachSize + (ulong)seenHash.Count;
            }
            return sumOfUniqueHashesForEachSize;
        }

        [Test]
        public void GetDupePairs_CheckAllDupeFilesHaveFullHash_OK()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();

            var d = new Duplication();
            var dupePairEnum = d.GetDupePairs(rootEntries);

            foreach (var dupe in dupePairEnum)
            {
                foreach (var de in dupe.Value)
                {
                    if (de.DirEntry.IsPartialHash)
                    {
                        Console.WriteLine("Trouble partial hash {0}", de.FilePath);
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
            var d = new Duplication();

            d.ApplyMd5Checksum(rootEntries);
        }
        // ReSharper restore InconsistentNaming
    }
}
