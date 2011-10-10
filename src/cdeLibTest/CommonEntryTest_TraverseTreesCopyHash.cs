using System;
using cdeLib;
using NUnit.Framework;
using cdeLib.Infrastructure;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
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

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash, Is.EqualTo(new Hash16(09)));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash, Is.EqualTo(new Hash16(12)));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash, Is.EqualTo(new Hash16(13)));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash, Is.EqualTo(new Hash16(14)));
        }

        [Test]
        public void TraverseTreesCopyHash_FileNameDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Name = "different";

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.IsHashDone, Is.False);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash, Is.EqualTo(new Hash16(12)));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash, Is.EqualTo(new Hash16(13)));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash, Is.EqualTo(new Hash16(14)));
        }

        [Test]
        public void TraverseTreesCopyHash_FileDateDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Modified = new DateTime(2011, 02, 01, 12,11,10);

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.IsHashDone, Is.False);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash, Is.EqualTo(new Hash16(12)));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash, Is.EqualTo(new Hash16(13)));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash, Is.EqualTo(new Hash16(14)));
        }

        [Test]
        public void TraverseTreesCopyHash_FileSizeDifferent_DoesNotCopyFilesHash()
        {
            RecreateTestTree();
            _sde1.Size = 312;

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.IsHashDone, Is.False);
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash, Is.EqualTo(new Hash16(12)));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash, Is.EqualTo(new Hash16(13)));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash, Is.EqualTo(new Hash16(14)));
        }

        [Test]
        public void TraverseTreesCopyHash_DirDateDifferent_StillCopiesHashsInsideTree()
        {
            RecreateTestTree();
            _dfe4.Modified = new DateTime(2011, 02, 01, 12, 11, 10);

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash, Is.EqualTo(new Hash16(09)));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.Hash, Is.Not.Null); Assert.That(_dde5.Hash, Is.EqualTo(new Hash16(12)));
            Assert.That(_dde6.Hash, Is.Not.Null); Assert.That(_dde6.Hash, Is.EqualTo(new Hash16(13)));
            Assert.That(_dde7.Hash, Is.Not.Null); Assert.That(_dde7.Hash, Is.EqualTo(new Hash16(14)));
        }

        [Test]
        public void TraverseTreesCopyHash_DirNameDifferent_DoesNotCopyHashsUnderPath()
        {
            RecreateTestTree();
            _dfe4.Name = "different";

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash, Is.EqualTo(new Hash16(09)));
            Assert.That(_dde2.Hash, Is.Not.Null); Assert.That(_dde2.Hash, Is.EqualTo(new Hash16(10)));
            Assert.That(_dde3.Hash, Is.Not.Null); Assert.That(_dde3.Hash, Is.EqualTo(new Hash16(11)));
            Assert.That(_dde5.IsHashDone, Is.False);
            Assert.That(_dde6.IsHashDone, Is.False);
            Assert.That(_dde7.IsHashDone, Is.False);
        }

        [Test]
        public void TraverseTreesCopyHash_CopyHashIfSourceHasFullGasgAndDestHasPartialHash()
        {
            RecreateTestTree();
            _dde1.Hash = new Hash16(99);
            _dde1.IsPartialHash = true;

            _reSource.TraverseTreesCopyHash(_reDest);

            Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash, Is.EqualTo(new Hash16(09)));
        }

        [Test]
        public void TraverseTreesCopyHash_DontCopyHashIfDestHasFullHash()
        {
            RecreateTestTree();
            _dde1.SetHash(99);//_dde1.Hash = new byte[] { 99 };
            _dde1.IsPartialHash = false;

            _reSource.TraverseTreesCopyHash(_reDest);

            //Assert.That(_dde1.Hash, Is.Not.Null); Assert.That(_dde1.Hash[0], Is.EqualTo(99));
        }

        private void RecreateTestTree()
        {
            _reSource = new RootEntry { RootPath = @"C:\" };
            _sde1 = new DirEntry { Name = @"de1", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 01) }; _sde1.SetHash(09);
            _sde2 = new DirEntry { Name = @"de2", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 02) }; _sde2.SetHash(10);
            _sde3 = new DirEntry { Name = @"de3", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 03) }; _sde3.SetHash(11);
            _sfe4 = new DirEntry { IsDirectory = true, Name = @"fe4", Modified = new DateTime(2011, 02, 04) };
            _sde5 = new DirEntry { Name = @"de5", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 05) }; _sde5.SetHash(12);
            _sde6 = new DirEntry { Name = @"de6", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 06) }; _sde6.SetHash(13);
            _sde7 = new DirEntry { Name = @"de7", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 07) }; _sde7.SetHash(14);
            _reSource.Children.Add(_sde1);
            _reSource.Children.Add(_sde2);
            _reSource.Children.Add(_sde3);
            _reSource.Children.Add(_sfe4);
            _sfe4.Children.Add(_sde5);
            _sfe4.Children.Add(_sde6);
            _sfe4.Children.Add(_sde7);

            _reDest = new RootEntry { RootPath = @"C:\" };
            _dde1 = new DirEntry { Name = @"de1", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 01) };
            _dde2 = new DirEntry { Name = @"de2", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 02) };
            _dde3 = new DirEntry { Name = @"de3", Size = 10, IsPartialHash = false, Modified = new DateTime(2011, 02, 03) };
            _dfe4 = new DirEntry { IsDirectory = true, Name = @"fe4", Modified = new DateTime(2011, 02, 04) };
            _dde5 = new DirEntry { Name = @"de5", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 05) };
            _dde6 = new DirEntry { Name = @"de6", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 06) };
            _dde7 = new DirEntry { Name = @"de7", Size = 11, IsPartialHash = false, Modified = new DateTime(2011, 02, 07) };
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