using System.Collections.Generic;
using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CommonEntryTest_GetDirEntries
    {
        [Test]
        public void GetDirEntries_NoRootEntries_ReturnsEmptyEnumerable()
        {
            var rootEntries = new List<RootEntry>();

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.False);
        }

        [Test]
        public void GetDirEntries_EmptyRootEntry_ReturnsEmptyEnumerable()
        {
            var re = new RootEntry { RootPath = @"C:\" };
            var rootEntries = new List<RootEntry> { re };

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.False);
        }

        [Test]
        public void GetDirEntries_RootEntryOneDe_ReturnsEnumeratorWithAvailableData()
        {
            var re = new RootEntry { RootPath = @"C:\" };
            var de1 = new DirEntry { Name = @"de1" };
            re.Children.Add(de1);
            var rootEntries = new List<RootEntry> { re };

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.True);

            //foreach (var flatDE in CommonEntry.GetDirEntries(rootEntries))
            //{
            //}
        }

        [Test]
        public void GetDirEntries_RootEntryOneDe_ReturnsThatOneDe()
        {
            var re = new RootEntry {RootPath = @"C:\"};
            var de1 = new DirEntry {Name = @"de1"};
            re.Children.Add(de1);
            re.SetInMemoryFields();
            var rootEntries = new List<RootEntry> {re};
            
            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            var de = deEnum.First();
            Assert.That(de, Is.EqualTo(de1));
            Assert.That(de.FullPath, Is.EqualTo(@"C:\de1"));
        }

        [Ignore("Incomplete")]
        [Test]
        public void GetDirEntries_DirectoryWithOneFile_ReturnsDirThenFile()
        {
            //var re = new RootEntry { RootPath = @"C:\" };
            //var de1 = new DirEntry { Name = @"de1", IsDirectory = true };
            //var fe2 = new DirEntry { Name = @"fe2" };
            //de1.Children.Add(fe2);
            //re.Children.Add(de1);
            //re.SetInMemoryFields();
            //var rootEntries = new List<RootEntry> { re };


            //var deEnum = CommonEntry.GetDirEntries(rootEntries);
            //var expectEnum = expected.GetEnumerator();

            
            //while (deEnum.MoveNext() && expectEnum.MoveNext())
            //{
                
            //}
            
            //var fde = deEnum.First();
            //Assert.That(fde.DirEntry, Is.EqualTo(expectFde.DirEntry));
            //Assert.That(fde.FilePath, Is.EqualTo(expectFde.FilePath));
            //var fde = deEnum.Ne();
            //Assert.That(fde.DirEntry, Is.EqualTo(expectFde.DirEntry));
            //Assert.That(fde.FilePath, Is.EqualTo(expectFde.FilePath));
        }

        [Test]
        public void GetDirEnumerator_NoRootEntries_()
        {
            //var re = new RootEntry { RootPath = @"C:\" };
            //var de1 = new DirEntry { Name = @"de1", IsDirectory = true };
            //var fe2 = new DirEntry { Name = @"fe2" };
            //de1.Children.Add(fe2);
            //re.Children.Add(de1);
            //re.SetInMemoryFields();
            //var rootEntries = new List<RootEntry> { re };

        }
    }

    [TestFixture]
    public class GetDirEnumerator
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

    }
    // ReSharper restore InconsistentNaming
}