//using System;
//using System.Collections.Generic;
//using DocoptNet;
//using NUnit.Framework;
//using cde;

//namespace cdeLibTest
//{
//    [TestFixture]
//    class DocoptTest
//    {
//        public const string Usage1 = @"cde - catalog directory entries.
//Usage:
//  cde --stuff <path>...
//
//Optoins:
//  --stuff <path>...    do something";


//        [Test]
//        public void Basic_Multiparam_Options()
//        {
//            var args = "--stuff a1 a2";
//            var opts = new Docopt().Apply(Usage1, args, version: "learn docopt 1.0");
//            var val = opts["<path>"];
//            var asList = val.AsList;
//            var firstParam = asList[0];
//            var secondParam = asList[1];
//            DocoptDump(opts);
//            Assert.True(opts["--stuff"].IsTrue);
//            Assert.That(firstParam.ToString(), Is.EqualTo("a1"));
//            Assert.That(firstParam.ToString(), Is.EqualTo("a1"));
//            Assert.That(secondParam.ToString(), Is.EqualTo("a2"));
//        }

//        [Test]
//        public void CDEUsage_MultiPath_OK()
//        {
//            var input = "--scan a1 a2 a3";
//            var arguments = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//            foreach (var argument in arguments)
//            {
//                Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
//            }
//            //ValueObject b;
//            //var a = arguments["<path>"];
//            Assert.That(arguments["<path>"].AsList.Count, Is.EqualTo(3), "mooo");

//        }

//        [Test]
//        public void CDEUsage_Version_v()
//        {
//            var input = "-v";
//            Console.WriteLine("input \"{0}\"", input);
//            var opts = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//            DocoptDump(opts);
//            Assert.That(opts["-v"].AsInt, Is.EqualTo(1));
//        }

//        [Test]
//        public void CDEUsage_Version_version()
//        {
//            var input = "--version";
//            Console.WriteLine("input \"{0}\"", input);
//            //Console.WriteLine("usage \"{0}\"", Program.Usage);
//            //Console.WriteLine("--------------------");
//            var opts = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//            DocoptDump(opts);
//            Assert.That(opts["--version"].AsInt, Is.EqualTo(1));
//        }

//        [Test]
//        public void CDEUsage_Help()
//        {
//            var input = "-v";
//            try
//            {
//                Console.WriteLine("args \"{0}\"", input);
//                var arguments = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//                DocoptDump(arguments);
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine("----- Exception {0}", e);
//                Console.WriteLine(e.Message);
//                Console.WriteLine("----- Exception");
//            }
//        }

//        private static void DocoptDump(IDictionary<string, ValueObject> arguments)
//        {
//            Console.WriteLine("Docopt dump...");
//            foreach (var argument in arguments)
//            {
//                Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
//            }
//        }

//        [Test]
//        public void CDEUsage_find_excludeFiles_one_exclusion()
//        {
//            var input = "--dupes --excludeFiles wilbur";
//            Console.WriteLine("input \"{0}\"", input);
//            //Console.WriteLine("usage \"{0}\"", Program.Usage);
//            //Console.WriteLine("--------------------");
//            var opts = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//            DocoptDump(opts);
//            Assert.That(opts["--excludeFiles"].ToString(), Is.EqualTo("wilbur"));
//        }

//        [Test]
//        public void CDEUsage_find_excludeFiles_two_exclusions()
//        {
//            var input = "--dupes --excludeFiles wilbur --excludeFiles maxxy";
//            Console.WriteLine("input \"{0}\"", input);
//            //Console.WriteLine("usage \"{0}\"", Program.Usage);
//            //Console.WriteLine("--------------------");
//            var opts = new Docopt().Apply(Program.Usage, input, exit: false, help: false);
//            DocoptDump(opts);
//            Assert.That(opts["--excludeFiles"].ToString(), Is.EqualTo("wilbur"));
//        }

//  //app [(--with <more>)...] --dothing <files>... 

//        const string usageA =
//@"TestApp
//
//Usage:
//  app [(--with <more>)...] --dothing <files>... 
//
//  --dothing <files>...  MOOOOOO styff
//  --with <more>...      With more
//";

//        [Test]
//        public void DocOpt_Test_MultipleParameters_On_Option1()
//        {
//            var input = "--dothing fe fi fo fum";
//            DoTest(input, usageA);
//        }

//        [Test]
//        public void DocOpt_Test_MultipleParameters_On_Option2()
//        {
//            var input = "--with moo --with baa --dothing fe fi fo fum";
//            DoTest(input, usageA);
//        }

//        [Test]
//        public void DocOpt_Test_MultipleParameters_On_Option2b()
//        {
//            var input = "--with moo baa --dothing fe fi fo fum";
//            DoTest(input, usageA);
//        }

//        [Test]
//        public void DocOpt_Test_MultipleParameters_On_Option2_FAIL()
//        {
//            var input = "--dothing fe fi fo fum --with moo --with baa";
//            DoTest(input, usageA);
//        }


//        [Test]
//        public void DocOpt_Test_MultipleParameters_On_Option2x()
//        {
//            var input = "--dothing fe fi fo fum --with ert -with s";
//            DoTest(input, usageA);
//        }

//        private static void DoTest(string input, string usage)
//        {
//            Console.WriteLine("input \"{0}\"", input);
//            Console.WriteLine("usage \"{0}\"", usage);
//            var opts = new Docopt().Apply(usage, input, exit: false, help: false);
//            DocoptDump(opts);
//        }

//        private const string a =
//@"
//Options:
//  x--include <stuff>        Include these
//
//
//Options:
//  --with <stuff>...        Here
//
//";

//    }
//}
