using System;
using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CommonEntryTest_TraverseTreesCopyHash_SingleEntry
    {
        private RootEntry rootSource;
        private RootEntry rootDest;
        private DirEntry deSource;
        private DirEntry deDest;

        [SetUp]
        public void BeforeEveryTest()
        {
            SetupRootDestTest1();
        }

        private void SetupRootDestTest1()
        {
            rootSource = new RootEntry { RootPath = @"C:\" };
            deSource = GetNewTestF1();
            rootSource.Children.Add(deSource);

            rootDest = new RootEntry { RootPath = @"C:\" };
            deDest = GetNewTestF1();
            deDest.Hash = null;
            deDest.IsPartialHash = true;
            rootDest.Children.Add(deDest);
        }

        private static DirEntry GetNewTestF1()
        {
            return new DirEntry
                       {
                           Name = "f1",
                           Modified = new DateTime(2011, 05, 01, 12, 11, 10),
                           Size = 10,
                           IsPartialHash = true,
                           Hash = Encoding.UTF8.GetBytes("testhash")
                       };
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_MetaCopied()
        {
            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_NameDifference_NotCopied()
        {
            deSource.Name = "F1";

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_SizeDifference_NotCopied()
        {
            deSource.Size = 11;

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_ModifiedDifference_NotCopied()
        {
            deSource.Modified = deDest.Modified.Add(new TimeSpan(10));

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasPartialMD5_DestHasPartialMD5_NotCopied()
        {
            deSource.Hash = Encoding.UTF8.GetBytes("Moo1");
            deDest.Hash = Encoding.UTF8.GetBytes("Bang");

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(Encoding.UTF8.GetBytes("Bang")));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasFullMD5_DestHasPartialMD5_Copied()
        {
            deSource.Hash = Encoding.UTF8.GetBytes("Moo1");
            deSource.IsPartialHash = false;
            deDest.Hash = Encoding.UTF8.GetBytes("Bang");

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(Encoding.UTF8.GetBytes("Moo1")));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_Lev2Found_Copied()
        {
            var deSourceLev2 = GetNewTestF1();
            var deDestLev2 = GetNewTestF1();
            deDestLev2.Hash = null;
            deDestLev2.IsPartialHash = false;

            deSource.IsDirectory = true;
            deSource.Children.Add(deSourceLev2);
            deDest.IsDirectory = true;
            deDest.Children.Add(deDestLev2);

            rootSource.TraverseTreesCopyHash(rootDest); // act

            Assert.That(deDestLev2.Hash, Is.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDestLev2.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_Lev2FoundDestDoesntMatch_NotCopied()
        {
            var deSourceLev2 = GetNewTestF1();
            var deDestLev2 = GetNewTestF1();
            deDestLev2.Hash = null;
            deDestLev2.IsPartialHash = false;
            deDestLev2.Name = "notsame";

            deSource.IsDirectory = true;
            deSource.Children.Add(deSourceLev2);
            deDest.IsDirectory = true;
            deDest.Children.Add(deDestLev2);

            rootSource.TraverseTreesCopyHash(rootDest); // act

            Assert.That(deDestLev2.Hash, Is.Not.EqualTo("testhash"));
            Assert.That(deDestLev2.IsPartialHash, Is.Not.EqualTo(true));
        }
    }
    // ReSharper restore InconsistentNaming
}