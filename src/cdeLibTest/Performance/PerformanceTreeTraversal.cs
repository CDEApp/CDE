using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using cdeLib;

namespace cdeLibTest.Performance
{
    /// <summary>
    /// Compare performance of.
    /// TraverseTreePair
    /// PairDirEntryEnumerator
    /// For both C:\ drive 200k entries, and C:\ and D:\ total 1.4 million entries.
    /// 
    /// Test run with sort() not enabled in PairDirEntryEnumerator.
    /// 
    /// </summary>
    [TestFixture]
    class PerformanceTreeTraversal
    {
        private const string PathToTest = @"..\..\..\..\test\";
        private const string TestCatalog200K = PathToTest + "C-Vertex3.cde";
        private const string TestCatalog1_2M = PathToTest + "D-SM15T_2_1.cde";

        // ReSharper disable InconsistentNaming
        // ReSharper disable JoinDeclarationAndInitializer
        // ReSharper disable PossibleMultipleEnumeration

        private int _repeatSmall = 100;
        private int _repeatLarge = 25;

        [SetUpFixture]
        public class TestData
        {
            public static RootEntry RootSmall { get; set; }
            public static int RootSmallCount;
            public static RootEntry RootLarge { get; set; }
            public static int RootLargeCount;

            [SetUp]
            public void BeforeAllTests()
            {
                RootSmall = RootEntry.LoadDirCache(TestCatalog200K);
                if (RootSmall == null)
                {
                    throw new Exception("TestData setup failed cannot load " + TestCatalog200K);
                }
                var e1 = CommonEntry.GetDirEntries(RootSmall);
                RootSmallCount = e1.Count();

                RootLarge  = RootEntry.LoadDirCache(TestCatalog1_2M);
                if (RootSmall == null)
                {
                    throw new Exception("TestData setup failed cannot load " + TestCatalog1_2M);
                }
                var e2 = CommonEntry.GetDirEntries(RootLarge);
                RootLargeCount = e2.Count();
            }
        }

        [SetUp]
        public void BeforeEachTest()
        {
            if (TestData.RootSmall == null)
            {
                Console.WriteLine("RootSmall null oops");
                return;
            }
            if (TestData.RootLarge == null)
            {
                Console.WriteLine("RootLarge null oops");
                return;
            }
        }

        [Ignore("Just a manually run performance test.")]
        [Test]
        public void TestCSV()
        {
            long msecs;

            Console.WriteLine("TestName,RepeatCount,EntryCount,TotalTime");

            msecs = DoPairDirEntryEnumeratorCountTest(TestData.RootSmall, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "PairDirEntryEnumerator Small Count", _repeatSmall, TestData.RootSmallCount, msecs);
            msecs = DoPairDirEntryEnumeratorCountTest(TestData.RootLarge, _repeatLarge);
            Console.WriteLine("{0},{1},{2},{3}", "PairDirEntryEnumerator Large Count", _repeatLarge, TestData.RootLargeCount, msecs);

            msecs = DoPairDirEntryEnumeratorTest(TestData.RootSmall, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "PairDirEntryEnumerator Small Make List", _repeatSmall, TestData.RootSmallCount, msecs);
            msecs = DoPairDirEntryEnumeratorTest(TestData.RootLarge, _repeatLarge);
            Console.WriteLine("{0},{1},{2},{3}", "PairDirEntryEnumerator Large Make List", _repeatLarge, TestData.RootLargeCount, msecs);

            msecs = DoDirEntryEnumeratorTest(TestData.RootSmall, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "DirEntryEnumerator Small Make List", _repeatSmall, TestData.RootSmallCount, msecs);
            msecs = DoDirEntryEnumeratorTest(TestData.RootLarge, _repeatLarge);
            Console.WriteLine("{0},{1},{2},{3}", "DirEntryEnumerator Large Make List", _repeatLarge, TestData.RootLargeCount, msecs);

            _repeatSmall *= 10;
            _repeatLarge *= 10;
            msecs = DoTraverseTreePairTest(TestData.RootSmall, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "TraverseTreePair Small Make List", _repeatSmall, TestData.RootSmallCount, msecs);
            msecs = DoTraverseTreePairTest(TestData.RootLarge, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "TraverseTreePair Large Make List", _repeatLarge, TestData.RootLargeCount, msecs);

            msecs = DoTraverseTreeTest(TestData.RootSmall, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "TraverseTree Small Make List", _repeatSmall, TestData.RootSmallCount, msecs);
            msecs = DoTraverseTreeTest(TestData.RootLarge, _repeatSmall);
            Console.WriteLine("{0},{1},{2},{3}", "TraverseTree Large Make List", _repeatLarge, TestData.RootLargeCount, msecs);
        }

        public long DoPairDirEntryEnumeratorCountTest(RootEntry root, int repeatCount)
        {
            var sw = new Stopwatch();
            sw.Start();
            var rootEntries = new List<RootEntry> { root };
            var pairDirEntries = CommonEntry.GetPairDirEntries(rootEntries);
            for (var i = 0; i < repeatCount; i++)
            {
                pairDirEntries.Count();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public long DoPairDirEntryEnumeratorTest(RootEntry root, int repeatCount)
        {
            var sw = new Stopwatch();
            sw.Start();
            var rootEntries = new List<RootEntry> { root };
            var pairDirEntries = CommonEntry.GetPairDirEntries(rootEntries);
            for (var i = 0; i < repeatCount; i++)
            {
                pairDirEntries.ToList();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public long DoDirEntryEnumeratorTest(RootEntry root, int repeatCount)
        {
            var sw = new Stopwatch();
            sw.Start();
            var rootEntries = new List<RootEntry> { root };
            var pairDirEntries = CommonEntry.GetDirEntries(rootEntries);
            for (var i = 0; i < repeatCount; i++)
            {
                pairDirEntries.ToList();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public long DoTraverseTreePairTest(RootEntry root, int repeatCount)
        {
            var sw = new Stopwatch();
            sw.Start();
            var testlist = new List<PairDirEntry>();
            ((CommonEntry) root).TraverseTreePair((p, d) => testlist.Add(new PairDirEntry(p, d)));
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public long DoTraverseTreeTest(RootEntry root, int repeatCount)
        {
            var sw = new Stopwatch();
            sw.Start();
            var testlist = new List<DirEntry>();
            ((CommonEntry) root).TraverseTreePair((p, d) => testlist.Add(d));
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }


        private static RootEntry MeasureLoad(string catalogName)
        {
            var sw = new Stopwatch();
            sw.Start();
            var reC = RootEntry.LoadDirCache(catalogName);
            sw.Stop();
            var loadTime = sw.ElapsedMilliseconds;
            if (reC == null)
            {
                Console.WriteLine("Not Loaded!");
                return null;
            }
            Console.WriteLine("Loaded!");
            Console.WriteLine("loadTime " + loadTime + " msecs");
            return reC;
        }

        [Ignore("Just a manually run performance test.")]
        [Test]
        public void SetCommonEntryFields_Test_Small()
        {
            DoSetCommonEntryFields(TestCatalog200K, 100);
        }

        [Ignore("Just a manually run performance test.")]
        [Test]
        public void SetCommonEntryFields_Test_Large()
        {
            DoSetCommonEntryFields(TestCatalog1_2M, 35);
        }

        public void DoSetCommonEntryFields(string catalogName, int repeatCount)
        {
            Stopwatch sw;
            long totalTraverseTime;
            double traverseTimeAverage;
            var reC = MeasureLoad(catalogName);

            sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < repeatCount; i++)
            {
                reC.SetCommonEntryFields();
            }
            sw.Stop();
            totalTraverseTime = sw.ElapsedMilliseconds;
            traverseTimeAverage = 1.0d * totalTraverseTime/repeatCount;
            Console.WriteLine("SetCommonEntryFields");
            Console.WriteLine("repeatCount " + repeatCount);
            Console.WriteLine("totalTraverseTime " + totalTraverseTime + " msecs");
            Console.WriteLine("traverseTimeAverage " + traverseTimeAverage + " msecs");

            sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < repeatCount; i++)
            {
               //this code was temporary and showed no real gain.
               //reC.SetCommonEntryFieldsT();
            }
            sw.Stop();
            totalTraverseTime = sw.ElapsedMilliseconds;
            traverseTimeAverage = 1.0d * totalTraverseTime/repeatCount;
            Console.WriteLine("SetCommonEntryFieldsNoParent");
            Console.WriteLine("repeatCount " + repeatCount);
            Console.WriteLine("totalTraverseTime " + totalTraverseTime + " msecs");
            Console.WriteLine("traverseTimeAverage " + traverseTimeAverage + " msecs");
        }
        // ReSharper restore PossibleMultipleEnumeration
        // ReSharper restore JoinDeclarationAndInitializer
        // ReSharper restore InconsistentNaming
    }


    // 
    // TODO.... test load without set ParentCommonEntry begin done
    // i think its pretty slow, load of D: 1.2M feels slower than i remember.
    // can probaly do it lazilly in PairDirEnum if its not set ?
    // -- field only used DirEntry.GetListFromRoot()
    //     which could be done by using fullpath - and walking down from root.
    //
}
