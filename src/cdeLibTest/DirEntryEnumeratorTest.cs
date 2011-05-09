using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class DirEntryEnumeratorTest
    {
        private RootEntry _re1;
        private DirEntry _de1;
        private DirEntry _fe2;
        private List<RootEntry> _rootEntries;

        [SetUp]
        public void RunBeforeEveryTest()
        {
            RebuildTestRoot();
        }

        public void RebuildTestRoot()
        {
            _re1 = new RootEntry { RootPath = @"C:\" };
            _de1 = new DirEntry { Name = @"de1", IsDirectory = true };
            _fe2 = new DirEntry { Name = @"fe2" };
            _de1.Children.Add(_fe2);
            _re1.Children.Add(_de1);
            _re1.SetInMemoryFields();
            _rootEntries = new List<RootEntry> { _re1 };
        }

        [Test]
        public void Constructor_Minimal_NoErrors()
        {
            _rootEntries = new List<RootEntry>();

            new DirEntryEnumerator(_rootEntries);
        }

        [Test]
        public void MoveNext_NoRootEntries_FirstMoveNextFalse()
        {
            _rootEntries = new List<RootEntry>();

            var e = new DirEntryEnumerator(_rootEntries);

            Assert.That(e.Current, Is.Null);
            Assert.That(e.MoveNext(), Is.False);
        }

        [Test]
        public void MoveNext_WithTree_CurrentIsNullBeforeMoveNext()
        {
            var e = new DirEntryEnumerator(_rootEntries);

            Assert.That(e.Current, Is.Null);
        }

        [Test]
        public void MoveNext_WithTree_FirstMoveNextIsTrue()
        {
            var e = new DirEntryEnumerator(_rootEntries);

            Assert.That(e.MoveNext(), Is.True);
        }

        [Test]
        public void MoveNext_WithTree_AfterMoveNextCurrentHasFirstEntry()
        {
            var e = new DirEntryEnumerator(_rootEntries);
            e.MoveNext();

            Assert.That(e.Current, Is.EqualTo(_de1), "Did not get de1 entry.");
        }

        [Test]
        public void MoveNext_WithTree_AfterTwoMoveNextCurrentHasSecondEntry()
        {
            var e = new DirEntryEnumerator(_rootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.Current, Is.EqualTo(_fe2), "Did not get fe2 entry.");
        }

        [Test]
        public void MoveNext_WithTree_ThirdMoveNextReturnsFalse()
        {
            var e = new DirEntryEnumerator(_rootEntries);
            e.MoveNext();
            e.MoveNext();

            Assert.That(e.MoveNext(), Is.False, "There are only supposed to be 2 entries");
        }

        [Test]
        public void ListOfRootEntryTest_TryOutEnumerable()
        {
            var fe3 = new DirEntry { Name = "fe3" };
            _de1.Children.Add(fe3);
            var myListEnumerator = new DirEntryEnumerator(_rootEntries);

            var expectList = new List<DirEntry> { _de1, _fe2, fe3 };
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
}