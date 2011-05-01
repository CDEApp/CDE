using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure
{

    public class CRC32 : IHashAlgorithm
    {
        uint[] tab;

        public CRC32()
        {
            Init(0x04c11db7);
        }

        public CRC32(uint poly)
        {
            Init(poly);
        }

        void Init(uint poly)
        {
            tab = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint t = i;
                for (int j = 0; j < 8; j++)
                    if ((t & 1) == 0)
                        t >>= 1;
                    else
                        t = (t >> 1) ^ poly;
                tab[i] = t;
            }
        }

        public uint Hash(byte[] data)
        {
            uint hash = 0xFFFFFFFF;
            foreach (byte b in data)
                hash = (hash << 8) ^ tab[b ^ (hash >> 24)];
            return ~hash;
        }
    }

    public class SHA1Wrapper : IHashAlgorithm
    {
        private SHA1 sha1;
        public SHA1Wrapper()
        {
            sha1 = SHA1.Create();
        }


        public uint Hash(byte[] data)
        {
            var result = sha1.ComputeHash(data);
            return BitConverter.ToUInt32(result,0);
        }
    }

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
//            var murmurHash2Simple = new MurmurHash2Simple();
//            var murmurHash2Unsafe = new MurmurHash2Unsafe();
//            var murmur2HashInline = new MurmurHash2InlineBitConverter();
            //var hashHelper = new MD5Hash();
            var sha1 = new SHA1Wrapper();
            var crc32 = new CRC32();
//            var murmer2UInt32Hack = new MurmurHash2UInt32Hack();
            
//            tests.Add("murmurHash2Simple",murmurHash2Simple);
//            tests.Add("murmurHash2Unsafe",murmurHash2Unsafe);
//            tests.Add("murmerHash2Inline", murmur2HashInline);
//            tests.Add("murmerHash2UInt32Hack", murmer2UInt32Hack);
            
            //tests.Add("md5.NET", hashHelper);
            tests.Add("Sha1",sha1);
            tests.Add("CRC32", crc32);
            


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