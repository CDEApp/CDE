using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    class CommonEntryTest_SetSummaryFields //: RootEntryTestBase
    {
        private CommonEntry.DirStats dirStats;
        private CommonEntry emptyCommonEntry;

        [SetUp]
        public void BeforeEveryTest()
        {
            emptyCommonEntry = new CommonEntryTestStub();
            dirStats = new CommonEntry.DirStats();
        }

        [Test]
        public void SetSummaryFields_DirCount_Zero_For_Empty_CommonEntry()
        {
            emptyCommonEntry.SetSummaryFields(dirStats);
            Assert.That(dirStats.DirCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_FileCount_Zero_For_Empty_CommonEntry()
        {
            emptyCommonEntry.SetSummaryFields(dirStats);
            Assert.That(dirStats.FileCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_Entry_Size_Zero_For_Empty_CommonEntry()
        {
            emptyCommonEntry.SetSummaryFields(dirStats);
            Assert.That(emptyCommonEntry.Size, Is.EqualTo(0));
        }

        //[Test]
        //public void SetSummaryFields_DirEntryCount_Zero_For_Empty_CommonEntry()
        //{
        //    emptyCommonEntry.SetSummaryFields(dirStats);
        //    Assert.That(emptyCommonEntry.DirEntryCount, Is.EqualTo(0));
        //}

        //[Test] public void SetSummaryFields_FileEntryCount_Zero_For_Empty_CommonEntry()
        //{
        //    emptyCommonEntry.SetSummaryFields(dirStats);
        //    Assert.That(emptyCommonEntry.FileEntryCount, Is.EqualTo(0));
        //}

    }

    class CommonEntryTest_SetSummaryFields2 : RootEntryTestBase
    {
        DirEntry de2a;
        DirEntry de2b;
        DirEntry de2c;
        DirEntry de3a;
        DirEntry de4a;
        private RootEntry re;
        CommonEntry.DirStats dirStats;

        [SetUp]
        public void BeforeEveryTest()
        {
            dirStats = new CommonEntry.DirStats();
            re = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
        }

        [Test]
        public void SetSummaryFields_FileCount_AsExpected()
        {
            var testEntry = re;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(dirStats.FileCount, Is.EqualTo(3));
        }

        [Test]
        public void SetSummaryFields_DirCount_AsExpected()
        {
            var testEntry = re;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(dirStats.DirCount, Is.EqualTo(2));
        }

        [Test]
        public void SetSummaryFields_EntrySize_Correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(testEntry.Size, Is.EqualTo(28));
        }

        [Test]
        public void SetSummaryFields_de3a_entry_size_correct()
        {
            var testEntry = de3a;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        //[Test]
        //public void SetSummaryFields_de3a_DirEntryCount_correct()
        //{
        //    var testEntry = de3a;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.DirEntryCount, Is.EqualTo(0));
        //}

        //[Test]
        //public void SetSummaryFields_de3a_FileEntryCount_correct()
        //{
        //    var testEntry = de3a;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.FileEntryCount, Is.EqualTo(1));
        //}

        [Test]
        public void SetSummaryFields_de3a_Size_correct()
        {
            var testEntry = de3a;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        //[Test]
        //public void SetSummaryFields_de2b_DirEntryCount_correct()
        //{
        //    var testEntry = de2b;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.DirEntryCount, Is.EqualTo(1));
        //}

        //[Test]
        //public void SetSummaryFields_de2b_FileEntryCount_correct()
        //{
        //    var testEntry = de2b;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.FileEntryCount, Is.EqualTo(1));
        //}

        [Test]
        public void SetSummaryFields_de2b_Size_correct()
        {
            var testEntry = de2b;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        //[Test]
        //public void SetSummaryFields_re_DirEntryCount_correct()
        //{
        //    var testEntry = re;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.DirEntryCount, Is.EqualTo(2));
        //}

        //[Test]
        //public void SetSummaryFields_re_FileEntryCount_correct()
        //{
        //    var testEntry = re;
        //    testEntry.SetSummaryFields(dirStats);

        //    Assert.That(testEntry.FileEntryCount, Is.EqualTo(3));
        //}

        [Test]
        public void SetSummaryFields_re_Size_correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields(dirStats);

            Assert.That(testEntry.Size, Is.EqualTo(28));
        }
    }
    // ReSharper restore InconsistentNaming
}