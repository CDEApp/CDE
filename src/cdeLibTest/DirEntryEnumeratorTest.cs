using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cdeLib;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest
{
    public class DirEntryTestBase
    {
        private RootEntry _re1;
        protected DirEntry De1;
        protected DirEntry Fe2;
        protected List<RootEntry> RootEntries;

        protected void RebuildTestRoot(IConfiguration config)
        {
            _re1 = new RootEntry(config) { Path = @"C:\" };
            De1 = new DirEntry(true) { Path = "de1" };
            Fe2 = new DirEntry { Path = "fe2" };
            De1.Children.Add(Fe2);
            _re1.Children.Add(De1);
            _re1.SetInMemoryFields();
            RootEntries = new List<RootEntry> { _re1 };
        }
    }

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class DirEntryEnumeratorTest : DirEntryTestBase
    {
        readonly IConfiguration _config = Substitute.For<IConfiguration>();

        [SetUp]
        public void RunBeforeEveryTest()
        {
            _config.ProgressUpdateInterval.Returns(5000);
            RebuildTestRoot(_config);
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
            var fe3 = new DirEntry { Path = "fe3" };
            De1.Children.Add(fe3);
            var myListEnumerator = new DirEntryEnumerator(RootEntries);

            var expectList = new List<DirEntry> { De1, Fe2, fe3 };
            var expectEnumerator = expectList.GetEnumerator();

            while (myListEnumerator.MoveNext() && expectEnumerator.MoveNext())
            {
                Console.WriteLine($"a {myListEnumerator.Current.Path}");
                if (expectEnumerator.Current != null)
                {
                    Console.WriteLine($"b {expectEnumerator.Current.Path}");
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
            System.Threading.Thread.Sleep(200); // msec
            //_num = 0;
            _fileCount = 0;
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < 100; ++i)
            {
                var deEnumerator = CommonEntry.GetDirEntries(rootEntries);
                foreach (var dirEntry in deEnumerator)
                {
                    //_num += (ulong)dirEntry.FullPath.Length;
                    ++_fileCount;
                    //if (Hack.BreakConsoleFlag)
                    //{
                    //    Console.WriteLine("\nBreak key detected exiting full TraverseTree inner.");
                    //    break;
                    //}
                }
            }
            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds/10:00}";
            Console.WriteLine($"Enumerator took : {elapsedTime}");
            Console.WriteLine($"Total files enumerated : {_fileCount}");
            //Console.WriteLine("Total path length : {0}", _num);

            var re = rootEntries.First();
            sw.Reset();
            sw.Start();
            //_num = 0;
            _fileCount = 0;
            for (var i = 0; i < 100; ++i)
            {
                re.TraverseTreePair((p, d) => { ++_fileCount; return true; });
            }
            sw.Stop();
            ts = sw.Elapsed;
            elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds/10:00}";
            Console.WriteLine($"TraverseTreePair took : {elapsedTime}");
            Console.WriteLine($"Total files enumerated : {_fileCount}");
            //Console.WriteLine("Total path length : {0}", _num);
        }
    }
    // ReSharper restore InconsistentNaming
}