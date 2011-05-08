using System;
using System.Collections.Generic;
using cdeLib;
using NUnit.Framework;
using Rhino.Mocks;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class CommonEntryTest
    {
        [Test]
        public void Constructor_Minimal_OK()
        {
            var a = new CommonEntryTestStub();

            Assert.That(a, Is.Not.Null);
        }

        [Test]
        public void TraverseTree_EmptyTree_ActionNotRun()
        {
            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            mockAction.Stub(x => x("", null));
            var de = new DirEntry();

            de.TraverseTree("", mockAction);

            mockAction.AssertWasNotCalled(x => x("", null));
        }

        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild()
        {
            var de1 = new DirEntry { Name = "d1" };// only looks at Children for recurse.
            var de2 = new DirEntry { Name = "d2" };
            de1.Children.Add(de2);

            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            mockAction.Stub(x => x("d2", de2));

            de1.TraverseTree("", mockAction);

            mockAction.AssertWasCalled(x => x("d2", de2));
        }

        [Test]
        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildren()
        {
            var re1 = new RootEntry { RootPath = "" };
            var de2a = new DirEntry { Name = "d2a" };
            var de2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var de2c = new DirEntry { Name = "d2c" };
            re1.Children.Add(de2a);
            re1.Children.Add(de2b);
            re1.Children.Add(de2c);
            var de3a = new DirEntry { Name = "d3a", IsDirectory = true };
            de2b.Children.Add(de3a);
            var de4a = new DirEntry { Name = "d4a" };
            de3a.Children.Add(de4a);

            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(@"d2a", de2a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b", de2b)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2c", de2c)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a", de3a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a\d4a", de4a)).Repeat.Times(1);
            }

            re1.TraverseTree("", mockAction);

            mockAction.VerifyAllExpectations();
        }

        [Test]
        public void TraverseAllTrees_TreeHeirarchy_CallsActionOnAllChildrenOnBothRoots()
        {
            var re1 = new RootEntry { RootPath = "" };
            var de2a = new DirEntry { Name = "d2a" };
            var de2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var de2c = new DirEntry { Name = "d2c" };
            re1.Children.Add(de2a);
            re1.Children.Add(de2b);
            re1.Children.Add(de2c);
            var de3a = new DirEntry { Name = "d3a", IsDirectory = true };
            de2b.Children.Add(de3a);
            var de4a = new DirEntry { Name = "d4a" };
            de3a.Children.Add(de4a);

            // same structure as re1 with a different root path
            var re2 = new RootEntry { RootPath = "2" };
            var bde2a = new DirEntry { Name = "d2a" };
            var bde2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var bde2c = new DirEntry { Name = "d2c" };
            re2.Children.Add(bde2a);
            re2.Children.Add(bde2b);
            re2.Children.Add(bde2c);
            var bde3a = new DirEntry { Name = "d3a", IsDirectory = true };
            bde2b.Children.Add(bde3a);
            var bde4a = new DirEntry { Name = "d4a" };
            bde3a.Children.Add(bde4a);

            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(@"d2a", de2a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b", de2b)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2c", de2c)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a", de3a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a\d4a", de4a)).Repeat.Times(1);

                mockAction.Expect(x => x(@"2\d2a", bde2a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"2\d2b", bde2b)).Repeat.Times(1);
                mockAction.Expect(x => x(@"2\d2c", bde2c)).Repeat.Times(1);
                mockAction.Expect(x => x(@"2\d2b\d3a", bde3a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"2\d2b\d3a\d4a", bde4a)).Repeat.Times(1);
            }

            CommonEntry.TraverseAllTrees(new List<RootEntry> { re1, re2 }, mockAction);

            mockAction.VerifyAllExpectations();
        }

    }

    [TestFixture]
    class CommonEntryTest_TraverseTreesCopyHash
    {
        private RootEntry _reSource;
        private DirEntry _sde1;
        private DirEntry _sde2;
        private DirEntry _sde3;
        private DirEntry _sfe4;
        private DirEntry _sde5;
        private DirEntry _sde6;
        private DirEntry _sde7;

        private RootEntry _reDest;
        private DirEntry _dde1;
        private DirEntry _dde2;
        private DirEntry _dde3;
        private DirEntry _dfe4;
        private DirEntry _dde5;
        private DirEntry _dde6;
        private DirEntry _dde7;

        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "source and destination must be not null.")]
        [Test]
        public void TraverseTreesCopyHash_RunWithDirEntry_Exception()
        {
            var reSource = new DirEntry { Name = @"Moo" };

            reSource.TraverseTreesCopyHash(null);
        }

        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "source and destination must have same root path.")]
        [Test]
        public void TraverseTreesCopyHash_RootPathsDifferent_Exception()
        {
            var reSource = new RootEntry { RootPath = @"C:\a" };
            var reDest = new RootEntry { RootPath = @"C:\" };

            reSource.TraverseTreesCopyHash(reDest);
        }

        [Test]
        public void TraverseTreesCopyHash_AllMatch_CopyAllHash()
        {
            RecreateTestTree();

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(09));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash[0], Is.EqualTo(12));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash[0], Is.EqualTo(13));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash[0], Is.EqualTo(14));
        }

        [Test]
        public void TraverseTreesCopyHash_FileNameDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Name = "different";

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Null);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash[0], Is.EqualTo(12));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash[0], Is.EqualTo(13));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash[0], Is.EqualTo(14));
        }

        [Test]
        public void TraverseTreesCopyHash_FileDateDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Modified = new DateTime(2011, 02, 01, 12,11,10);

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Null);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash[0], Is.EqualTo(12));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash[0], Is.EqualTo(13));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash[0], Is.EqualTo(14));
        }

        [Test]
        public void TraverseTreesCopyHash_FileSizeDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Size = 312;

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Null);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash[0], Is.EqualTo(12));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash[0], Is.EqualTo(13));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash[0], Is.EqualTo(14));
        }

        [Test]
        public void TraverseTreesCopyHash_DirDateDifferent_StillCopiesHashsInsideTree()
        {
            RecreateTestTree();
            _dfe4.Modified = new DateTime(2011, 02, 01, 12, 11, 10);

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(09));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash[0], Is.EqualTo(12));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash[0], Is.EqualTo(13));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash[0], Is.EqualTo(14));
        }

        [Test]
        public void TraverseTreesCopyHash_DirNameDifferent_DoesNotCopyHashsUnderPath()
        {
            RecreateTestTree();
            _dfe4.Name = "different";

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(09));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash[0], Is.EqualTo(10));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash[0], Is.EqualTo(11));
            Assert.That(_dde5.Hash, Is.Null);
            Assert.That(_dde6.Hash, Is.Null);
            Assert.That(_dde7.Hash, Is.Null);
        }

        [Test]
        public void TraverseTreesCopyHash_CopyHashIfSourceHasFullGasgAndDestHasPartialHash()
        {
            RecreateTestTree();
            _dde1.Hash = new byte[] { 99 };
            _dde1.IsPartialHash = true;

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(09));
        }

        [Test]
        public void TraverseTreesCopyHash_DontCopyHashIfDestHasFullHash()
        {
            RecreateTestTree();
            _dde1.Hash = new byte[] { 99 };
            _dde1.IsPartialHash = false;

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(99));
        }

        private void RecreateTestTree()
        {
            _reSource = new RootEntry { RootPath = @"C:\" };
            _sde1 = new DirEntry { Name = @"de1", Size = 10, Hash = new byte[] { 09 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 01) };
            _sde2 = new DirEntry { Name = @"de2", Size = 10, Hash = new byte[] { 10 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 02) };
            _sde3 = new DirEntry { Name = @"de3", Size = 10, Hash = new byte[] { 11 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 03) };
            _sfe4 = new DirEntry { IsDirectory = true, Name = @"fe4", Modified = new DateTime(2011, 02, 04) };
            _sde5 = new DirEntry { Name = @"de5", Size = 11, Hash = new byte[] { 12 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 05) };
            _sde6 = new DirEntry { Name = @"de6", Size = 11, Hash = new byte[] { 13 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 06) };
            _sde7 = new DirEntry { Name = @"de7", Size = 11, Hash = new byte[] { 14 }, IsPartialHash = false, Modified = new DateTime(2011, 02, 07) };
            _reSource.Children.Add(_sde1);
            _reSource.Children.Add(_sde2);
            _reSource.Children.Add(_sde3);
            _reSource.Children.Add(_sfe4);
            _sfe4.Children.Add(_sde5);
            _sfe4.Children.Add(_sde6);
            _sfe4.Children.Add(_sde7);

            _reDest = new RootEntry { RootPath = @"C:\" };
            _dde1 = new DirEntry { Name = @"de1", Size = 10, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 01) };
            _dde2 = new DirEntry { Name = @"de2", Size = 10, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 02) };
            _dde3 = new DirEntry { Name = @"de3", Size = 10, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 03) };
            _dfe4 = new DirEntry { IsDirectory = true, Name = @"fe4", Modified = new DateTime(2011, 02, 04) };
            _dde5 = new DirEntry { Name = @"de5", Size = 11, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 05) };
            _dde6 = new DirEntry { Name = @"de6", Size = 11, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 06) };
            _dde7 = new DirEntry { Name = @"de7", Size = 11, Hash = null, IsPartialHash = false, Modified = new DateTime(2011, 02, 07) };
            _reDest.Children.Add(_dde1);
            _reDest.Children.Add(_dde2);
            _reDest.Children.Add(_dde3);
            _reDest.Children.Add(_dfe4);
            _dfe4.Children.Add(_dde5);
            _dfe4.Children.Add(_dde6);
            _dfe4.Children.Add(_dde7);
        }
    }
    // ReSharper restore InconsistentNaming
}
