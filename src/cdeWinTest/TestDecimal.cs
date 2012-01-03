using System;
using NUnit.Framework;

namespace cdeWinTest
{
    [TestFixture]
    class TestDecimal
    {
        [Ignore("Testing i goofed on a cast elsewhere.")]
        [Test]
        public void TestDecimalTimesSomething()
        {
            decimal d = 6.001m;
            decimal d2 = 6.000m;
            int mb = 1024*1024;

            Console.WriteLine("d*mb " + d*mb);
            Console.WriteLine("d2*mb " + d2*mb);
            Console.WriteLine("(long)d*mb " + (long)d*mb);
            Console.WriteLine("(long)d2*mb " + (long)d2*mb);

        }
    }
}
