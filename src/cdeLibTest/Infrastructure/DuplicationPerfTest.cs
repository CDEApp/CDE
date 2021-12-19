using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using cdeLib.Infrastructure.Hashing;
using cdeLibTest.Infrastructure.Hashing;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure
{
    public class DuplicationPerfTest
    {
        /// <summary>
        /// Benchmark hashers.
        /// md5.NET:	  572.68 MB/s(4365)
        /// Sha1:		  708.35 MB/s(3529)
        /// CRC32:		  235.76 MB/s(10603)
        /// MurmurHash3: 2903.31 MB/s(861)
        /// </summary>
        [Test, Explicit("Only for benchmarking")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public void PerformanceHashTest()
        {
            var tests = new Dictionary<string, IHashAlgorithm>
            {
                {"md5.NET", new MD5Hash()},
                {"Sha1", new Sha1Wrapper()},
                {"CRC32", new Crc32()},
                {"MurmurHash3", new MurmurHashWrapper()}
            };

            var data = new byte[256 * 1024];
            new Random().NextBytes(data);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            if (Environment.ProcessorCount > 1)
            {
                Process.GetCurrentProcess().ProcessorAffinity =
                    new IntPtr(1 << (Environment.ProcessorCount - 1));
            }

            foreach (var (hashKey, hashMethod) in tests)
            {
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < 9999; i++)
                {
                    hashMethod.Hash(data);
                }

                timer.Stop();
                Console.WriteLine(
                    $"{hashKey}:\t\t{(data.Length * (1000.0 / (timer.ElapsedMilliseconds / 9999.0))) / (1024.0 * 1024.0):F2} MB/s ({timer.ElapsedMilliseconds})");
            }
        }
    }
}