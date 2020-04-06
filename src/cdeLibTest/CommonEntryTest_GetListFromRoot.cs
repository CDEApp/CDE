using cdeLib;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    class CommonEntryTest_GetListFromRoot
    {
        readonly IConfiguration _config = Substitute.For<IConfiguration>();

        public void Setup()
        {
            _config.ProgressUpdateInterval.Returns(5000);
        }

        [Test]
        public void GetListFromRoot_FromRoot_SameAsRootEntry()
        {
            var re = new RootEntry(_config) { Path = @"X:\" };

            var list = re.GetListFromRoot();

            Assert.That(list[0].Path, Is.EqualTo(@"X:\"));
        }

        [Test]
        public void GetListFromRoot_FirstLevelEntry_TwoItemsReturned()
        {
            var re = new RootEntry(_config) {Path = @"X:\"};
            var de1 = new DirEntry {Path = "de1" };
            re.Children.Add(de1);
            re.SetCommonEntryFields();

            var list = de1.GetListFromRoot();

            Assert.That(list[0].Path, Is.EqualTo(@"X:\"));
            Assert.That(list[1].Path, Is.EqualTo("de1"));
        }
    }
    // ReSharper restore InconsistentNaming
}
