using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    class DirEntryTest_SetSummaryFields
    {
        private DirEntry emptyDirEntry;

        [SetUp]
        public void BeforeEveryTest()
        {
            emptyDirEntry = new DirEntry(true);
        }

        [Test]
        public void SetSummaryFields_DirCount_Zero_For_Empty_DirEntry()
        {
            emptyDirEntry.SetSummaryFields();
            Assert.That(emptyDirEntry.DirEntryCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_FileCount_Zero_For_Empty_DirEntry()
        {
            emptyDirEntry.SetSummaryFields();
            Assert.That(emptyDirEntry.FileEntryCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_Entry_Size_Zero_For_Empty_DirEntry()
        {
            emptyDirEntry.SetSummaryFields();
            Assert.That(emptyDirEntry.Size, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_DirEntryCount_Zero_For_Empty_DirEntry()
        {
            emptyDirEntry.SetSummaryFields();
            Assert.That(emptyDirEntry.DirEntryCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_FileEntryCount_Zero_For_Empty_DirEntry()
        {
            emptyDirEntry.SetSummaryFields();
            Assert.That(emptyDirEntry.FileEntryCount, Is.EqualTo(0));
        }

    }

    class DirEntryTest_SetSummaryFields2 : RootEntryTestBase
    {
        DirEntry de2a;
        DirEntry de2b;
        DirEntry de2c;
        DirEntry de3a;
        DirEntry de4a;
        private RootEntry re;

        [SetUp]
        public void BeforeEveryTest()
        {
            re = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
        }

        [Test]
        public void SetSummaryFields_FileCount_AsExpected()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.FileEntryCount, Is.EqualTo(3));
        }

        [Test]
        public void SetSummaryFields_DirCount_AsExpected()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.DirEntryCount, Is.EqualTo(2));
        }

        [Test]
        public void SetSummaryFields_EntrySize_Correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.Size, Is.EqualTo(28));
        }

        [Test]
        public void SetSummaryFields_de3a_entry_size_correct()
        {
            var testEntry = de3a;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        [Test]
        public void SetSummaryFields_de3a_DirEntryCount_correct()
        {
            var testEntry = de3a;
            testEntry.DirEntryCount = -1;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.DirEntryCount, Is.EqualTo(0));
        }

        [Test]
        public void SetSummaryFields_de3a_FileEntryCount_correct()
        {
            var testEntry = de3a;
            testEntry.DirEntryCount = -1;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.FileEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void SetSummaryFields_de3a_Size_correct()
        {
            var testEntry = de3a;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        [Test]
        public void SetSummaryFields_de2b_DirEntryCount_correct()
        {
            var testEntry = de2b;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.DirEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void SetSummaryFields_de2b_FileEntryCount_correct()
        {
            var testEntry = de2b;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.FileEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void SetSummaryFields_de2b_Size_correct()
        {
            var testEntry = de2b;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.Size, Is.EqualTo(17));
        }

        [Test]
        public void SetSummaryFields_TestRootEntry_DirEntryCount_correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.DirEntryCount, Is.EqualTo(2));
        }

        [Test]
        public void SetSummaryFields_TestRootEntry_FileEntryCount_correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.FileEntryCount, Is.EqualTo(3));
        }

        [Test]
        public void SetSummaryFields_TestRootEntry_Size_correct()
        {
            var testEntry = re;
            testEntry.SetSummaryFields();

            Assert.That(testEntry.Size, Is.EqualTo(28));
        }
    }
    // ReSharper restore InconsistentNaming
}