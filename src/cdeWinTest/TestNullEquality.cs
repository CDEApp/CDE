using NUnit.Framework;

namespace cdeWinTest
{
    [TestFixture]
    class TestNullEquality
    {
        [Test]
        public void TestNullEquality_True()
        {
            // ReSharper disable EqualExpressionComparison
            Assert.That((null == null), Is.True, "SQL is hauting me, verifying.");
            // ReSharper restore EqualExpressionComparison
        }
    }
}
