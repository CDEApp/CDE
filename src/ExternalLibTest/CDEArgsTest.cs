using System;
using NDesk.Options;
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
            Assert.That(cdeArgs.Error, Is.EqualTo("The -grep option is not supported in mode '-scan'."));
        }

        [Test]
        public void PathEnabled_Not_Supported_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-path" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -path option is not supported in mode '-scan'."));
        }

        [Test]
        public void ReplEnabled_Not_Supported_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-repl" };
            var cdeArgs = new CDEArgs(args);
            Console.WriteLine("error \"{0}\"", cdeArgs.Error);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -repl option is not supported in mode '-scan'."));
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
            Assert.That(cdeArgs.Error, Is.EqualTo("The -basePath option is not supported in mode '-scan'."));
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

        [Test]
        public void HashAll_Enabled_Hash()
        {
            var args = new[] { "-hash", "-hashAll" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.HashAllEnabled, Is.EqualTo(true));
        }

        [Test]
        public void HashAll_Not_Supported_Find()
        {
            var args = new[] { "-find", @"C:\", "-hashAll" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo("The -hashAll option is not supported in mode '-find'."));
            Assert.That(cdeArgs.HashAllEnabled, Is.EqualTo(false));
        }

        [Test]
        public void Exclude_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-exclude", "Moooo" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.Exclude[0], Is.EqualTo("Moooo"));
        }

        [Test]
        public void Include_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-include", "XoomXoom" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.Include[0], Is.EqualTo("XoomXoom"));
        }

        [Test]
        public void Alternate_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-alternate" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.Alternate, Is.EqualTo(true));
        }

        [Test]
        public void MinSize_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minSize", "234" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinSize, Is.EqualTo(234));
        }

        [Test]
        public void MinSize_With_KB_Multiplier_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minSize", "234KB" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinSize, Is.EqualTo(234000));
        }

        [Test]
        public void MinSize_With_MB_Multiplier_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minSize", "234MB" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinSize, Is.EqualTo(234000000));
        }

        [Test]
        public void MinSize_With_GB_Multiplier_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minSize", "234GB" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinSize, Is.EqualTo(234000000000));
        }

        [Test]
        public void MaxSize_With_KB_Multiplier_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-maxSize", "14KB" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MaxSize, Is.EqualTo(14000));
        }

        [Test]
        public void MinDate_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minDate", "2012-02-29T10:15:30" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinDate, Is.EqualTo(new DateTime(2012, 02, 29, 10, 15, 30)));
        }

        [Test]
        public void MinDate_With_Missing_Value()
        {
            var args = new[] { "-scan", @"C:\", "-minDate" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo("Missing required value for option '-minDate'."));
        }

        [Test]
        public void MinDate_With_Bad_Value()
        {
            var args = new[] { "-scan", @"C:\", "-minDate", "" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo("Require Year parameter be a 4 Digit Year <YYYY> as part of format '<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>'"));
        }

        [Test]
        public void MinDate_With_Bad_Value2()
        {
            var args = new[] { "-scan", @"C:\", "-minDate", "2012-02-30" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo("Require valid Day of Month integer range 1-31 for Day <DD> as part of format '<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>'"));
        }

        [Test]
        public void MaxDate_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-maxDate", "2012-02-29T10:15:30" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MaxDate, Is.EqualTo(new DateTime(2012, 02, 29, 10, 15, 30)));
        }

        [Test]
        public void MinTime_Has_Value_Scan()
        {
            var args = new[] { "-scan", @"C:\", "-minTime", "10:15:30" };
            var cdeArgs = new CDEArgs(args);
            Assert.That(cdeArgs.Error, Is.EqualTo(null));
            Assert.That(cdeArgs.MinTime, Is.EqualTo(new DateTime(1, 1, 1, 10, 15, 30)));
        }
    }

}