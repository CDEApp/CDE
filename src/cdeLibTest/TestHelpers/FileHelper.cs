using System.IO;
using NUnit.Framework;

namespace cdeLibTest.TestHelpers
{
    using System;
    using System.Linq;

    public static class FileHelper
    {
        private static readonly Random Random = new Random();

        private static readonly string ProjectPath =
            Path.GetDirectoryName(
                Path.GetDirectoryName(Path.GetDirectoryName(TestContext.CurrentContext.TestDirectory)));
        
        public static readonly string TestDir = Path.Combine(ProjectPath, "Test");
        
        public static readonly string TestDir2 = Path.Combine(ProjectPath, "Test2");

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Next(s.Length)]).ToArray());
        }

    }
}