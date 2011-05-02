using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Alphaleonis.Win32.Filesystem;
using cdeLib;
using cdeLib.Infrastructure;
using NUnit.Framework;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;

namespace cdeLibTest.Infrastructure
{
    public class HashBenchmark
    {
        public long lengthAccumulator = 0;
        public long cnt;
        private HashHelper _hashHelper;

        [Test]
        public void TestHashPerfOnDisk()
        {
            _hashHelper = new HashHelper();
            var sw = new Stopwatch();
            sw.Start();
            Hash("C:\\temp",1);
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            Console.WriteLine("Jason's crap md5");
            ShowResult(sw, ts);

            sw = new Stopwatch();
            cnt = 0;
            lengthAccumulator = 0;
            sw.Start();
            Hash("C:\\temp", 2);
            sw.Stop();
            ts = sw.Elapsed;
            Console.WriteLine("Straight md5");
            ShowResult(sw, ts);

        }

        private void ShowResult(Stopwatch sw, TimeSpan ts)
        {
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("Hash took : {0} and processed {1}MB {2:F2} MB/s", elapsedTime, lengthAccumulator / (1024 * 1024),
                              ((lengthAccumulator / (sw.ElapsedMilliseconds / 1000.0)) / (1024 * 1024)));
        }

        public void Hash(string path, int method)
        {
            cnt += 1;

            if (cnt > 5)
                return;
            var hashHelper = new MD5Hash();
            int count = 0;
            foreach (var x in Directory.GetFiles(path))
            {
                if (method ==1)
                    Method1(x);
                else
                    Method2(x);
            }
            foreach (var d in Directory.GetDirectories(path))
            {
                Hash(d, method);
            }
        }

        private void Method2(string x)
        {
            using (var file = new FileStream(x, FileMode.Open, FileAccess.Read))
            {
                    
                MD5 md5 = MD5.Create();
                lengthAccumulator += file.Length;
                var hash = md5.ComputeHash(file);
            }
        }

        private void Method1(string x)
        {
            var hashResponse = _hashHelper.GetMD5HashFromFile(x);
            lengthAccumulator += hashResponse.BytesHashed;
        }
    }
}