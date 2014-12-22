using System;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace ExternalLibTest
{
    [TestFixture]
    public class CDEArgsTest_Basic_Modes
    {
        [Test]
        public void Mode_Scan_With_Parameter()
        {
            var args = new[] { "-scan", @"C:\" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Scan));
            Assert.That(cdeArgs.ScanParameters[0], Is.EqualTo(@"C:\"));
        }

        [Test]
        public void Mode_Find_With_Parameter()
        {
            var args = new[] { "-find", @"C:\" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Find));
            Assert.That(cdeArgs.FindParameters[0], Is.EqualTo(@"C:\"));
        }

        [Test]
        public void Mode_Hash()
        {
            var args = new[] { "-hash" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Hash));
        }

        [Test]
        public void Mode_Dupes()
        {
            var args = new[] { "-dupes" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Dupes));
        }

        [Test]
        public void Mode_Dump()
        {
            var args = new[] { "-dump" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Dump));
        }

        [Test]
        public void Mode_Help_And_Example_Help()
        {
            var args = new[] { "-h" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Help));
            cdeArgs.ShowHelpX();
        }

        [Test]
        public void Mode_Version()
        {
            var args = new[] { "-version" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.Version));
        }

        [Test]
        public void Mode_LoadWait()
        {
            var args = new[] { "-loadWait" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Mode, Is.EqualTo(CDEArgs.Modes.LoadWait));
        }

        [Test]
        public void Mode_Scan_Missing_Required_Parameter_Value()
        {
            var args = new[] { "-scan" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("Missing required value for option '-scan'."));
        }

        [Test]
        public void Mode_Find_Missing_Required_Parameter_Value()
        {
            var args = new[] { "-find" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("Missing required value for option '-find'."));
        }

        [Test]
        public void Set_Two_Modes_Gives_Error()
        {
            var args = new[] { "-scan", @"C:\", "-find", "hello" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("Only one Mode argument is allowed '-find'."));
        }

        [Test]
        public void Extra_UnMatched_Options_Cause_Error()
        {
            var args = new[] { "-dupes", "-notExist" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("Error unmatched parameter: '-notExist'"));
        }
    }

    [TestFixture]
    public class CDEArgsTest_Options
    {
        [Test]
        public void GrepEnabled_Not_Supported_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-grep" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -grep parameter is not supported in mode '-scan'."));
        }

        [Test]
        public void PathEnabled_Not_Supported_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-path" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -path parameter is not supported in mode '-scan'."));
        }

        [Test]
        public void ReplEnabled_Not_Supported_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-repl" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -repl parameter is not supported in mode '-scan'."));
        }

        [Test]
        public void StartPath_Has_Values_For_Find()
        {
            var args = new[] { "-find", @"C:\", "-basePath", @"X:\Moo" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.BasePaths[0], Is.EqualTo(@"X:\Moo"));
        }

        [Test]
        public void StartPath_Not_Supported_For_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-basePath", @"X:\Moo", @"Y:\Moo" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -basePath parameter is not supported in mode '-scan'."));
        }

        [Test]
        public void Grep_Enabled_Find()
        {
            var args = new[] { "-find", @"C:\", "-grep" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.GrepEnabled, Is.EqualTo(true));
        }

        [Test]
        public void Repl_Enabled_Find()
        {
            var args = new[] { "-find", @"C:\", "-repl" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.ReplEnabled, Is.EqualTo(true));
        }

        [Test]
        public void Path_Enabled_Find()
        {
            var args = new[] { "-find", @"C:\", "-path" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.PathEnabled, Is.EqualTo(true));
        }
    }
}