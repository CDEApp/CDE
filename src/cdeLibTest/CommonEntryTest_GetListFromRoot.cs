using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    class CommonEntryTest_GetListFromRoot
    {
        [Test]
        public void GetListFromRoot_FromRoot_SameAsRootEntry()
        {
            var re = new RootEntry { Path = @"X:\" };

            var list = re.GetListFromRoot();

            Assert.That(list[0].Path, Is.EqualTo(@"X:\"));
        }

        [Test]
        public void GetListFromRoot_FirstLevelEntry_TwoItemsReturned()
        {
            var re = new RootEntry {Path = @"X:\"};
            var de1 = new DirEntry {Path = @"de1" };
            re.Children.Add(de1);
            re.SetCommonEntryFields();

            var list = de1.GetListFromRoot();

            Assert.That(list[0].Path, Is.EqualTo(@"X:\"));
            Assert.That(list[1].Path, Is.EqualTo(@"de1"));
        }
    }
    // ReSharper restore InconsistentNaming
}
