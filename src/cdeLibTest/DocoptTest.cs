using System;
using DocoptNet;
using NUnit.Framework;
using cde;

namespace cdeLibTest
{
    [TestFixture]
    class DocoptTest
    {
        public const string Usage1 = @"cde - catalog directory entries.
Usage:
  cde --stuff <path>...

Optoins:
  --stuff <path>...    do something";


        [Test]
        public void Basic_Multiparam_Options()
        {
            var args = "--stuff a1 a2";
            var arguments = new Docopt().Apply(Usage1, args, version: "learn docopt 1.0");
            foreach (var argument in arguments)
            {
                Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
            }
        }

        [Test]
        public void CDEUsage_MultiPath_OK()
        {
            var args = "--scan a1 a2 a3";
            var arguments = new Docopt().Apply(Program.Usage, args, version: "learn docopt 1.0");
            foreach (var argument in arguments)
            {
                Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
            }
            //ValueObject b;
            //var a = arguments["<path>"];
            Assert.That(arguments["<path>"].AsList.Count, Is.EqualTo(3), "mooo");

        }

        [Test]
        public void CDEUsage_Help()
        {
            var args = "-h";
            try
            {
                var arguments = new Docopt().Apply(Program.Usage, args, version: "learn docopt 1.0", exit: true);
            }
            catch (Exception e)
            {
                Console.WriteLine("-----");
                Console.WriteLine(e.Message);
                Console.WriteLine("-----");
            }
            //foreach (var argument in arguments)
            //{
            //    Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
            //}
        }
    }
}
