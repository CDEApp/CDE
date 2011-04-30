using NUnit.Framework;

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
        public void Method_Something_Result()
        {
            Assert.Fail("Not done yet");
        }
    }
    // ReSharper restore InconsistentNaming
}
