using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure
{
    public class DuplicationTests
    {
        /// <summary>
        /// MurmerUnsafe should be the fastest.
        /// murmurHash2Simple:        267.27 MB/s (9353)
        /// murmurHash2Unsafe:        439.32 MB/s (5690)
        /// hashHelper (md5):        223.23 MB/s (11198)
        /// </summary>
        [Test]
        public void PerformanceHashTest()
        {
            var tests = new Dictionary<string, IHashAlgorithm>();
            var murmurHash2Simple = new MurmurHash2Simple();
            var murmurHash2Unsafe = new MurmurHash2Unsafe();
            var hashHelper = new MD5Hash();
            tests.Add("murmurHash2Simple",murmurHash2Simple);
            tests.Add("murmurHash2Unsafe",murmurHash2Unsafe);
            tests.Add("md5.NET", hashHelper);


            var data = new Byte[256 * 1024];
            new Random().NextBytes(data);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            if (Environment.ProcessorCount > 1)
            {
                Process.GetCurrentProcess().ProcessorAffinity =
                      new IntPtr(1 << (Environment.ProcessorCount - 1));
            }
            foreach (var testSubject in tests)
            {
                Stopwatch timer = Stopwatch.StartNew();
                for (int i = 0; i < 9999; i++)
                {
                    testSubject.Value.Hash(data);
                }
                timer.Stop();
                Console.WriteLine("{0}:\t\t{1:F2} MB/s ({2})", testSubject.Key,
                    (data.Length * (1000.0 / (timer.ElapsedMilliseconds / 9999.0)))
                        / (1024.0 * 1024.0),
                    timer.ElapsedMilliseconds);
            }
        }
        
    }
}