using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
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