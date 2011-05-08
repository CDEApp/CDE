using System;
using System.Collections.Generic;
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
    // ReSharper restore InconsistentNaming
}