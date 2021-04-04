using System.IO;
using NUnit.Framework;

namespace cdeLibTest.TestHelpers
{
    using System;
    using System.Linq;

    public static class FileHelper
    {
        private static readonly Random Random = new();

        private static readonly string ProjectPath =
            Path.GetDirectoryName(
                Path.GetDirectoryName(Path.GetDirectoryName(TestContext.CurrentContext.TestDirectory)));

        public static readonly string TestDir = Path.Combine(ProjectPath, "Test");

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        public static void WriteAllText(string data, string path, string fileName)
        {
            var fullFilePath = Path.Combine(path, fileName);
            File.WriteAllText(fullFilePath, data);
        }

        public static void WriteFile(byte[] data, string path, string fileName)
        {
            var fullFilePath = Path.Combine(path, fileName);
            var fs = new FileStream(fullFilePath, FileMode.Create);
            WriteFile(data, fs);
        }

        private static void WriteFile(byte[] data, Stream fs)
        {
            BinaryWriter bw;
            using (bw = new BinaryWriter(fs))
            {
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
        }
    }
}