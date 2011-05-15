using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    public class DirEntryTestBase
    {
        protected RootEntry Re1;
        protected DirEntry De1;
        protected DirEntry Fe2;
        protected List<RootEntry> RootEntries;

        public void RebuildTestRoot()
        {
            Re1 = new RootEntry { RootPath = @"C:\" };
            De1 = new DirEntry { Name = @"de1", IsDirectory = true };
            Fe2 = new DirEntry { Name = @"fe2" };
            De1.Children.Add(Fe2);
            Re1.Children.Add(De1);
            Re1.SetInMemoryFields();
            RootEntries = new List<RootEntry> { Re1 };
        }
    }

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class DirEntryEnumeratorTest : DirEntryTestBase
    {
        [SetUp]
        public void RunBeforeEveryTest()
        {
            RebuildTestRoot();
        }

        [Test]
        public void Constructor_Minimal_NoErrors()
        {
            RootEntries = new List<RootEntry>();

            new DirEntryEnumerator(RootEntries);
        }

        [Test]
        public void MoveNext_NoRootEntries_FirstMoveNextFalse()
        {
            RootEntries = new List<RootEntry>();

            var e = new DirEntryEnumerator(RootEntries);

            Assert.That(e.Current, Is.Null);
            Assert.That(e.MoveNext(), Is.False);
        }

        [Test]
        public void MoveNext_WithTree_CurrentIsNullBeforeMoveNext()
        {
            var e = new DirEntryEnumerator(RootEntries);

            Assert.That(e.Current, Is.Null);
        }

        [Test]
        public void MoveNext_WithTree_FirstMoveNextIsTrue()
        {
            var e = new DirEntryEnumerator(RootEntries);

            Assert.That(e.MoveNext(), Is.True);
        }

        [Test]
        public void MoveNext_WithTree_AfterMoveNextCurrentHasFirstEntry()
        {
            var e = new DirEntryEnumerator(RootEntries);
            e.MoveNext();

            Assert.That(e.Current, Is.EqualTo(De1), "Did not get de1 entry.");
        }

        [Test]
        public void MoveNext_WithTree_AfterTwoMoveNextCurrentHasSecondEntry()
        {
            var e = new DirEntryEnumerator(RootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.Current, Is.EqualTo(Fe2), "Did not get fe2 entry.");
        }

        [Test]
        public void MoveNext_WithTree_ThirdMoveNextReturnsFalse()
        {
            var e = new DirEntryEnumerator(RootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.MoveNext(), Is.False, "There are only supposed to be 2 entries");
        }

        [Test]
        public void ListOfRootEntryTest_TryOutEnumerable()
        {
            var fe3 = new DirEntry { Name = "fe3" };
            De1.Children.Add(fe3);
            var myListEnumerator = new DirEntryEnumerator(RootEntries);

            var expectList = new List<DirEntry> { De1, Fe2, fe3 };
            var expectEnumerator = expectList.GetEnumerator();

            while (myListEnumerator.MoveNext() && expectEnumerator.MoveNext())
            {
                Console.WriteLine("a {0}", myListEnumerator.Current.Name);
                if (expectEnumerator.Current != null)
                {
                    Console.WriteLine("b {0}", expectEnumerator.Current.Name);
                    Assert.That(myListEnumerator.Current, Is.EqualTo(expectEnumerator.Current), "Sequence of directory entries is not matching.");
                }
            }
        }
    }

    /// <summary>
    /// Totall floored, the enumerator is heaps and heaps and heaps faster.... ?
    /// </summary>
    [TestFixture]
    public class TestPerformance_TraverseTree_DirEntryEnumerator
    {
        private ulong _num;
        private ulong _fileCount;

        [Ignore("Its a performance test needs .cde file in current dir.")]
        [Test]
        public void PerformanceTest_Compare_TraverseTree_With_DirEntryEnumerator()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();

            _num = 0;
            _fileCount = 0;
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < 100; ++i)
            {
                var deEnumerator = CommonEntry.GetDirEntries(rootEntries);
                foreach (var dirEntry in deEnumerator)
                {
                    _num += (ulong)dirEntry.FullPath.Length;
                    ++_fileCount;
                    if (Hack.BreakConsoleFlag)
                    {
                        Console.WriteLine("\nBreak key detected exiting full TraverseTree inner.");
                        break;
                    }
                }
            }
            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("took : {0}", elapsedTime);
            Console.WriteLine("Total files enumerated : {0}", _fileCount);
            Console.WriteLine("Total path length : {0}", _num);

            var re = rootEntries.First();
            sw.Start();
            _num = 0;
            _fileCount = 0;
            for (var i = 0; i < 100; ++i)
            {
                re.TraverseTree(DoAction);
            }
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("took : {0}", elapsedTime);
            Console.WriteLine("Total files enumerated : {0}", _fileCount);
            Console.WriteLine("Total path length : {0}", _num);

            sw.Start();
            _num = 0;
            _fileCount = 0;
            for (var i = 0; i < 100; ++i)
            {
                re.TraverseTree2(DoAction2);
            }
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("took : {0}", elapsedTime);
            Console.WriteLine("Total files enumerated : {0}", _fileCount);
            Console.WriteLine("Total path length : {0}", _num);
        }

        private void DoAction(string fullPath, DirEntry dirEntry)
        {
            ++_fileCount;
            _num += (ulong)dirEntry.FullPath.Length;
        }

        private void DoAction2(DirEntry dirEntry)
        {
            ++_fileCount;
            _num += (ulong)dirEntry.FullPath.Length;
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class FDEEnumeratorTest : DirEntryTestBase
    {
        [SetUp]
        public void RunBeforeEveryTest()
        {
            RebuildTestRoot();
        }

        [Test]
        public void Constructor_Minimal_NoErrors()
        {
            RootEntries = new List<RootEntry>();

            new FDEEnumerator(RootEntries);
        }

        [Test]
        public void MoveNext_NoRootEntries_FirstMoveNextFalse()
        {
            RootEntries = new List<RootEntry>();

            var e = new FDEEnumerator(RootEntries);

            Assert.That(e.Current, Is.Null);
            Assert.That(e.MoveNext(), Is.False);
        }

        [Test]
        public void MoveNext_WithTree_CurrentIsNullBeforeMoveNext()
        {
            var e = new FDEEnumerator(RootEntries);

            Assert.That(e.Current, Is.Null);
        }

        [Test]
        public void MoveNext_WithTree_FirstMoveNextIsTrue()
        {
            var e = new FDEEnumerator(RootEntries);

            Assert.That(e.MoveNext(), Is.True);
        }

        [Test]
        public void MoveNext_WithTree_AfterMoveNextCurrentHasFirstEntry()
        {
            var e = new FDEEnumerator(RootEntries);
            e.MoveNext();

            Assert.That(e.Current.DirEntry, Is.EqualTo(De1), "Did not get de1 entry.");
            Assert.That(e.Current.FilePath, Is.EqualTo(@"C:\de1"), "Did not get de1 entry.");
        }

        [Test]
        public void MoveNext_WithTree_AfterTwoMoveNextCurrentHasSecondEntry()
        {
            var e = new FDEEnumerator(RootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.Current.DirEntry, Is.EqualTo(Fe2), "Did not get fe2 entry.");
            Assert.That(e.Current.FilePath, Is.EqualTo(@"C:\de1\fe2"), "Did not get fe2 entry.");
        }

        [Test]
        public void MoveNext_WithTree_ThirdMoveNextReturnsFalse()
        {
            var e = new FDEEnumerator(RootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.MoveNext(), Is.False, "There are only supposed to be 2 entries");
        }

        [Test]
        public void ListOfRootEntryTest_TryOutEnumerable()
        {
            var fe3 = new DirEntry { Name = "fe3" };
            De1.Children.Add(fe3);
            var myListEnumerator = new FDEEnumerator(RootEntries);

            var expectList2 = new List<FlatDirEntryDTO>
                {
                    new FlatDirEntryDTO(@"C:\de1", De1),
                    new FlatDirEntryDTO(@"C:\de1\fe2", Fe2),
                    new FlatDirEntryDTO(@"C:\de1\fe3", fe3)
                };

            /*var expectList = */ new List<DirEntry> { De1, Fe2, fe3 };
            var expectEnumerator = expectList2.GetEnumerator();

            while (myListEnumerator.MoveNext() && expectEnumerator.MoveNext())
            {
                Console.WriteLine("a {0}", myListEnumerator.Current.DirEntry.Name);
                if (expectEnumerator.Current != null)
                {
                    Console.WriteLine("b0 {0}", expectEnumerator.Current.DirEntry.Name);
                    Console.WriteLine("b1 {0}", expectEnumerator.Current.FilePath);
                    Assert.That(myListEnumerator.Current.DirEntry, Is.EqualTo(expectEnumerator.Current.DirEntry), "Sequence of directory entries is not matching on DirEntry.");
                    Assert.That(myListEnumerator.Current.FilePath, Is.EqualTo(expectEnumerator.Current.FilePath), "Sequence of directory entries is not matching on Fullpath.");
                }
            }
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class EntryEnumeratorTest : EntryTestBase
    {
        [SetUp]
        public void RunBeforeEveryTest()
        {
            RebuildTestRoot();
        }


        [Test]
        public void MoveNext_NoRootEntries_FirstMoveNextFalse()
        {
            var ee = new EntryEnumerator(EStore);

            Assert.That(ee.MoveNext(), Is.False);
        }

        [Test]
        public void MoveNext_WithTree_CurrentIsNullBeforeMoveNext()
        {
            var ee = new EntryEnumerator(EStore);

            Assert.That(ee.Current, Is.Null);
        }

        [Test]
        public void MoveNext_WithTree1_FirstMoveNextIsTrue()
        {
            AddEntries1();
            var ee = new EntryEnumerator(EStore);

            Assert.That(ee.MoveNext(), Is.True);
        }

        [Test]
        public void MoveNext_WithTree1_AfterMoveNextCurrentHasFirstEntry()
        {
            AddEntries1();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();

            Assert.That(ee.Current.Index, Is.EqualTo(File1Index), "Did not get File1Index entry.");
        }

        [Test]
        public void MoveNext_WithTree1_AfterSecondMoveNextReturnsFalse()
        {
            AddEntries1();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();

            Assert.That(ee.MoveNext(), Is.False);
        }

        [Test]
        public void MoveNext_WithTree1_AfterSecondMoveNextCurrentNull()
        {
            AddEntries1();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.Current, Is.Null);
        }

        [Test]
        public void MoveNext_WithTree2_AfterTwoMoveNextCurrentHasSecondEntry()
        {
            AddEntries1();
            AddEntries2();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.Current.Index, Is.EqualTo(File1Index), "Did not get File1Index entry.");
        }

        [Test]
        public void MoveNext_WithTree2_AfterThreeMoveNextCurrentHasThirdEntry()
        {
            AddEntries1();
            AddEntries2();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.Current.Index, Is.EqualTo(File3Index), "Did not get File3Index entry.");
        }

        [Test]
        public void MoveNext_WithTree2_FourthMoveNextReturnsFalse()
        {
            AddEntries1();
            AddEntries2();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.MoveNext(), Is.False, "Fouth MoveNext() true.");
        }

        [Test]
        public void MoveNext_WithTree3_FourthMoveNextReturnsTrue()
        {
            AddEntries1();
            AddEntries2();
            AddEntries3();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.MoveNext(), Is.True, "Fouth MoveNext() false,");
        }

        [Test]
        public void MoveNext_WithTree3_AfterFourMoveNextReturnsFourthEntry()
        {
            AddEntries1();
            AddEntries2();
            AddEntries3();
            var ee = new EntryEnumerator(EStore);
            ee.MoveNext();
            ee.MoveNext();
            ee.MoveNext();

            Assert.That(ee.Current.Index, Is.EqualTo(File4Index), "Did not get File4Index entry.");
        }
    }
    // ReSharper restore InconsistentNaming

    public class EntryTestBase
    {
        protected EntryStore EStore;
        protected int RootIndex;
        protected int File1Index;
        protected int Dir2Index;
        protected int File3Index;
        protected int File4Index;

        public void RebuildTestRoot()
        {
            EStore = new EntryStore();
            RootIndex = EStore.AddEntry(null, @"X:\", 0,
                new DateTime(2011, 05, 04, 10, 09, 08), true);

            EStore.Root = new RootEntry
            {
                RootIndex = RootIndex,
                DefaultFileName = "test1.cde",
                RootPath = @"X:\",
            };
        }

        // root(1) -> file1(2)
        public void AddEntries1()
        {
            // root(1) -> file1(2)
            File1Index = EStore.AddEntry("file1", null, 55,
                    new DateTime(2011, 05, 04, 10, 09, 07), parentIndex: RootIndex);
            Console.WriteLine("NextAvailableIndex 1 {0}", EStore.NextAvailableIndex);
        }

        // root(1) -> dir2(3), file1(2) # dir2(3) -> file3(4)
        public void AddEntries2()
        {
            // root(1) -> dir2(3), file1(2)
            Dir2Index = EStore.AddEntry("dir2", null, 0,
                    new DateTime(2011, 05, 04, 10, 09, 06), true, RootIndex); // woot fix.

            // root(1) -> dir2(3), file1(2) # dir2(3) -> file3(4)
            File3Index = EStore.AddEntry("file3", null, 66,
                    new DateTime(2011, 05, 04, 10, 09, 07), parentIndex: Dir2Index);
            Console.WriteLine("NextAvailableIndex 2 {0}", EStore.NextAvailableIndex);
        }

        // root(1) -> dir2(3), file1(2) # dir2(3) -> file3(4)
        public void AddEntries3()
        {
            // root(1) -> dir2(3), file1(2) # dir2(3) -> file4(5), file3(4)
            File4Index = EStore.AddEntry("file4", null, 77,
                                         new DateTime(2011, 05, 04, 10, 09, 05), parentIndex: Dir2Index);
            Console.WriteLine("NextAvailableIndex 3 {0}", EStore.NextAvailableIndex);
        }
    }
}