using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rhino.Mocks;
using Rhino.Mocks.Constraints;
using List = NUnit.Framework.List;

namespace cdeWinTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class TestEnumerable
    {
        // REFERENCE http://en.wikipedia.org/wiki/Language_Integrated_Query


        //[SetUp]
        //public void BeforeEveryTest()
        //{
        //}

        [Test]
        public void ElementAtOrDefault_OnIEnumerableInt_XXXX()
        {
            var list = new List<int>() { 1, 2, 3, 4 };
            foreach (var l in list)
            {
                Console.WriteLine(l);
            }
            //var secondIndex = indices.ElementAtOrDefault(2);
        }

        [Test]
        public void ElementAtOrDefault_OnIEnumerableInt()
        {
            var listA = new List<int>() { 1, 2, 3, 4 };
            var listB = new List<int>() { 1 };
            var listC = new List<int>() { 1, 0 };
            Moo(listA);
            Moo(listB); // same result phooey as listC
            Moo(listC);
            //var secondIndex = indices.ElementAtOrDefault(2);
        }

        public void Moo(IEnumerable<int> myEnum)
        {
            var a = myEnum.Take(2);
            //var secondIndex = myEnum.ElementAtOrDefault(1);
            Console.WriteLine(a.Count());
        }

        //public void Moo2(IEnumerable<int> myEnum)
        //{
        //    var a = myEnum.First();
        //    var a = myEnum.First();

        //    //var secondIndex = myEnum.ElementAtOrDefault(1);
        //    Console.WriteLine(secondIndex);
        //}
    }
    // ReSharper restore InconsistentNaming}
}
