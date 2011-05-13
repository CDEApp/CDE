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
        //private ulong _num;
        private ulong _fileCount;

        [Ignore("Its a performance test needs .cde file in current dir.")]
        [Test]
        public void PerformanceTest_Compare_TraverseTree_With_DirEntryEnumerator()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();

            //_num = 0;
            _fileCount = 0;
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < 100; ++i)
            {
                //var deEnumerator = CommonEntry.GetDirEntries(rootEntries);
                //foreach (var dirEntry in deEnumerator)
                {
                    //_num += (ulong)dirEntry.FullPath.Length;
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
            //Console.WriteLine("Total path length : {0}", _num);

            var re = rootEntries.First();
            sw.Start();
            //_num = 0;
            _fileCount = 0;
            for (var i = 0; i < 100; ++i)
            {
                re.TraverseTreePair(DoAction);
            }
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("took : {0}", elapsedTime);
            Console.WriteLine("Total files enumerated : {0}", _fileCount);
            //Console.WriteLine("Total path length : {0}", _num);

            sw.Start();
            //_num = 0;
            _fileCount = 0;
            for (var i = 0; i < 100; ++i)
            {
                re.TraverseTree(DoAction2);
            }
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("took : {0}", elapsedTime);
            Console.WriteLine("Total files enumerated : {0}", _fileCount);
            //Console.WriteLine("Total path length : {0}", _num);
        }

        private void DoAction(CommonEntry parentEntry, DirEntry dirEntry)
        {
            ++_fileCount;
            //_num += (ulong)dirEntry.FullPath.Length;
        }

        private void DoAction2(DirEntry dirEntry)
        {
            ++_fileCount;
            //_num += (ulong)dirEntry.FullPath.Length;
        }
    }
    // ReSharper restore InconsistentNaming
}