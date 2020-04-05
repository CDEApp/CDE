using System;
using System.Collections.Generic;
using System.Text;
using cdeLib;
using NUnit.Framework;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;

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
        IConfiguration _config = Substitute.For<IConfiguration>();

        [SetUp]
        public void BeforeEveryTest()
        {
            SetupRootDestTest1();
            _config.ProgressUpdateInterval.Returns(5000);
        }

        private void SetupRootDestTest1()
        {
            rootSource = new RootEntry(_config) { Path = @"C:\" };
            deSource = GetNewTestF1();
            rootSource.Children.Add(deSource);

            rootDest = new RootEntry(_config) { Path = @"C:\" };
            deDest = GetNewTestF1();
            deDest.SetHash(0);  // clear it just in case.
            deDest.IsHashDone = false;
            deDest.IsPartialHash = true;
            rootDest.Children.Add(deDest);
        }

        private static DirEntry GetNewTestF1()
        {
            var de = new DirEntry
                       {
                           Path = "f1",
                           Modified = new DateTime(2011, 05, 01, 12, 11, 10),
                           Size = 10,
                           IsPartialHash = true,
                       };
            de.SetHash(12312);
            return de;
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_MetaCopied()
        {
            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(new Hash16(12312)));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_NameDifference_NotCopied()
        {
            deSource.Path = "F1";

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(Encoding.UTF8.GetBytes("testhash")));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_SizeDifference_NotCopied()
        {
            deSource.Size = 11;

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(new Hash16(12312)));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasMD5_ModifiedDifference_NotCopied()
        {
            deSource.Modified = deDest.Modified.Add(new TimeSpan(10));

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.Not.EqualTo(new Hash16(12312)));
            Assert.That(deDest.IsPartialHash, Is.Not.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasPartialMD5_DestHasPartialMD5_NotCopied()
        {
            deSource.SetHash(12312);
            deDest.SetHash(12313);

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(new Hash16(12313)));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_OneEntrySourceHasFullMD5_DestHasPartialMD5_Copied()
        {
            //deSource.Hash = Encoding.UTF8.GetBytes("Moo1");
            deSource.SetHash(12312);
            deSource.IsPartialHash = false;
            //deDest.Hash = Encoding.UTF8.GetBytes("Bang");
            deDest.SetHash(12313);

            rootSource.TraverseTreesCopyHash(rootDest);

            Assert.That(deDest.Hash, Is.EqualTo(new Hash16(12312)));
            Assert.That(deDest.IsPartialHash, Is.EqualTo(false));
        }

        [Test]
        public void TraverseTreesCopyHash_Lev2Found_Copied()
        {
            var deSourceLev2 = GetNewTestF1();
            var deDestLev2 = GetNewTestF1();
            deDestLev2.IsHashDone = false;
            deDestLev2.IsPartialHash = false;

            deSource.IsDirectory = true;
            deSource.Children = new List<DirEntry>();
            deSource.Children.Add(deSourceLev2);
            deDest.IsDirectory = true;
            deDest.Children = new List<DirEntry>();
            deDest.Children.Add(deDestLev2);

            rootSource.TraverseTreesCopyHash(rootDest); // act

            Assert.That(deDestLev2.Hash, Is.EqualTo(new Hash16(12312)));
            Assert.That(deDestLev2.IsPartialHash, Is.EqualTo(true));
        }

        [Test]
        public void TraverseTreesCopyHash_Lev2FoundDestDoesntMatch_NotCopied()
        {
            var deSourceLev2 = GetNewTestF1();
            var deDestLev2 = GetNewTestF1();
            deDestLev2.IsHashDone = false;
            deDestLev2.IsPartialHash = false;
            deDestLev2.Path = "notsame";

            deSource.IsDirectory = true;
            deSource.Children = new List<DirEntry>();
            deSource.Children.Add(deSourceLev2);
            deDest.IsDirectory = true;
            deDest.Children = new List<DirEntry>();
            deDest.Children.Add(deDestLev2);

            rootSource.TraverseTreesCopyHash(rootDest); // act

            Assert.That(deDestLev2.Hash, Is.Not.EqualTo("testhash"));
            Assert.That(deDestLev2.IsPartialHash, Is.Not.EqualTo(true));
        }
    }
    // ReSharper restore InconsistentNaming
}