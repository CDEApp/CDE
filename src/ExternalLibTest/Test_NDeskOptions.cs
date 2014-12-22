using System;
using System.Collections.Generic;
using NDesk.Options;
using NUnit.Framework;

// ReSharper disable InconsistentNaming

namespace ExternalLibTest
{
    [TestFixture]
    public class Test_NDeskOptions
    {
        public List<string> testParseArgs(OptionSet p, string[] args, Action action)
        {
            try
            {
                Console.WriteLine("Trace OptionSet");
                p.WriteOptionDescriptions(Console.Out);
                Console.WriteLine("- - - - - - - - - -");

                var extra = p.Parse(args);
                if (extra.Count > 0)
                {
                    Console.WriteLine("ExtraParameters Params");
                    foreach (var e in extra)
                    {
                        Console.WriteLine("\"{0}\"", e);
                    }
                }
                action();
                return extra;
            }
            catch (OptionException)
            {
                Console.WriteLine("Error - usage is: {0}", p);
            }
            return null;
        }

        [Test]
        public void Basic_Arg_With_Parameter()
        {
            var scanTarget = String.Empty;
            var p = new OptionSet();
            p.Add("scan=", "scans given {Path} to catalog file", 
                o => scanTarget = o);
            var args = new[] { "-scan", @"c:\" };

            testParseArgs(p, args, 
                () => Assert.That(scanTarget, Is.EqualTo(@"c:\")));
        }

        [Test]
        public void Basic_Arg_With_Multi_Parameter()
        {
            var scanTarget = new List<string>();
            var p = new OptionSet();
            p.Add("scan=", "scans given {Path} to catalog file", 
                o => scanTarget.Add(o));
            var args = new[] { "-scan", @"c:\", "-scan", @"d:\" };

            var extras = testParseArgs(p, args,
                () => {
                    Assert.That(scanTarget[0], Is.EqualTo(@"c:\"));
                    Assert.That(scanTarget[1], Is.EqualTo(@"d:\")); 
                });
            Assert.That(extras, Is.Empty, "Left over arguments should not happen.");
        }

        [Test]
        public void Basic_Arg_With_Multi_Parameter_MultiPrefix_NotRequired()
        {
            var scanTarget = new List<string>();
            List<string> currentList = null;
            var p = new OptionSet();
            p.Add("scan=", "scans given {Path} to catalog file",
                o => {
                    currentList = scanTarget;
                    scanTarget.Add(o); 
                });
            p.Add("<>", "collect paths",
                o => {
                    if (currentList != null) // collect extra params till not scanFlag anymore
                    {
                        currentList.Add(o);
                    }
                });
            var args = new[] { "-scan", @"c:\", @"d:\" };

            var extras = testParseArgs(p, args,
                () => {
                    Assert.That(scanTarget[0], Is.EqualTo(@"c:\"));
                    Assert.That(scanTarget[1], Is.EqualTo(@"d:\")); 
                });
            Assert.That(extras, Is.Empty, "Left over arguments should not happen.");
        }

        [Test]
        public void Basic_Arg_With_Multi_Parameter_MultiPrefix_NotRequired_But_Works()
        {
            var scanTarget = new List<string>();
            List<string> currentList = null;
            var p = new OptionSet();
            p.Add("scan=", "scans given {Path} to catalog file",
                o => {
                    currentList = scanTarget;
                    scanTarget.Add(o); 
                });
            p.Add("<>", "collect paths",
                o => {
                    if (currentList != null) // collect extra params till not scanFlag anymore
                    {
                        currentList.Add(o);
                    }
                });
            var args = new[] { "-scan", @"c:\", "-scan", @"d:\" };

            var extras = testParseArgs(p, args,
                () => {
                    Assert.That(scanTarget[0], Is.EqualTo(@"c:\"));
                    Assert.That(scanTarget[1], Is.EqualTo(@"d:\")); 
                });
            Assert.That(extras, Is.Empty, "Left over arguments should not happen.");
        }

        [Test]
        public void Two_Multi_Args()
        {
            var scanTarget = new List<string>();
            var startPath = new List<string>();
            List<string> currentList = null;
            var p = new OptionSet();
            p.Add("scan=", "scans given {Path} to catalog file",
                o => {
                    currentList = scanTarget;
                    currentList.Add(o); 
                });
            p.Add("startPath=", "reference {Path} in catalogs to work from\n[not for -scan]",
                o =>
                {
                    currentList = startPath;
                    currentList.Add(o);
                });
            p.Add("<>", "collect multi string arguments",
                o => {
                    if (currentList != null)
                    {
                        currentList.Add(o);
                    }
                });
            var args = new[] { "-scan", @"c:\", @"d:\", "-startPath", @"C:\temp", @"C:\Users\rluiten" };

            var extras = testParseArgs(p, args,
                () => {
                    Assert.That(scanTarget[0], Is.EqualTo(@"c:\"));
                    Assert.That(scanTarget[1], Is.EqualTo(@"d:\"));
                    Assert.That(startPath[0], Is.EqualTo(@"C:\temp"));
                    Assert.That(startPath[1], Is.EqualTo(@"C:\Users\rluiten"));
                });
            Assert.That(extras, Is.Empty, "Left over arguments should not happen.");
        }

        // how to stop -startPath from being supported in -scan mode ?
        // there are some mutually exclusive modes.
        // scan, hash, dupes, find, grep, repl
        //   and   dump, loadwait
    }
}
