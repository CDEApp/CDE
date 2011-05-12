using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using cdeLib;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest
{

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class Hash16_Test
    {
        [Test]
        public void Constructor_Basic_OK()
        {
            var h = new Hash16();

            Assert.That(h.HashAsString, Is.EqualTo("00000000000000000000000000000000"));
        }

        [Test]
        public void Constructor_ValueComesOutAgainAsExpected_OK()
        {
            // ReSharper disable RedundantExplicitArraySize
            var b = new byte[16]
                {
                    0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                    0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
                };
            // ReSharper restore RedundantExplicitArraySize
            var s1 = ByteArrayHelper.ByteArrayToString(b);
            var s2 = new Hash16(b).HashAsString;
            Console.WriteLine("s1 {0}", s1);
            Console.WriteLine("s2 {0}", s2);
            Assert.That(s2, Is.EqualTo("ffeeddccbbaa99887766554433221100"));
        }

        [Test]
        public void HashAsString_Init1_OK()
        {
            // ReSharper disable RedundantExplicitArraySize
            var b = new byte[16]
                {
                    0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                    0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
                };
            // ReSharper restore RedundantExplicitArraySize

            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(b);
            }
            var h16 = new Hash16(hash);

            Console.WriteLine("h16.HashAsString {0}", h16.HashAsString);
            Console.WriteLine("         old hex {0}", ByteArrayHelper.ByteArrayToString(hash));

            Assert.That(h16.HashAsString, Is.EqualTo("a4bd60352d683c3eac5f826528bbfd02"));
        }
    }
    // ReSharper restore InconsistentNaming

    [TestFixture]
    class BAATreeTest
    {
        [Test]
        public void Test()
        {
            long GCMemStart = 0;
            long GCMemEnd = 0;
            long ProcessMemStart = 0;
            long ProcessMemEnd = 0;

            Process objProcess = Process.GetCurrentProcess();
            GCMemStart = GC.GetTotalMemory(true);
            ProcessMemStart = objProcess.PrivateMemorySize64;

            var bigArray = 1 * 1000 * 1000; //
            bigArray = 65536; //
            var store = new Entry[bigArray];
            //var store2 = new Entry[bigArray];
            //var store3 = new Entry[bigArray];
            //var store4 = new Entry[bigArray];

            for (int i = 0; i < bigArray; i++)
            {
                store[i].Size = (ulong)i;
                //store2[i].Size = (ulong)i;
                //store3[i].Size = (ulong)i;
                //store4[i].Size = (ulong)i;
            }
            Thread.Sleep(500);

            //Measure memory after allocating
            GCMemEnd = GC.GetTotalMemory(true);
            ProcessMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine("GC Memory Use:{0} (bytes)", GCMemEnd - GCMemStart);
            Console.WriteLine("Process Memory Use:{0} (bytes)", ProcessMemEnd - ProcessMemStart);
            Console.WriteLine("GC Memory Start:{0} (bytes)", GCMemStart.ToString());
            Console.WriteLine("Process Memory Start:{0} (bytes)", ProcessMemStart.ToString());
            Console.WriteLine("GC Memory End:{0} (bytes)", GCMemEnd.ToString());
            Console.WriteLine("x Process Memory End:{0} (bytes)", ProcessMemEnd.ToString());
            Console.WriteLine();
            //Console.ReadLine();
        }
    }
    [TestFixture]
    class EntryStoreTest
    {
        [Test]
        public void NewRootGetsCreatedAllocatesDirEntry()
        {
            EntryStore.Reset();
            var entryStore = new EntryStore();

            var rootIndex = EntryStore.NewRoot(@"C:\");

            Assert.That(rootIndex, Is.EqualTo(1));
            Assert.That(entryStore[rootIndex].FullPath, Is.EqualTo(@"C:\"));
            Assert.That(entryStore[rootIndex].Parent, Is.EqualTo(0));
            Assert.That(entryStore[rootIndex].IsDirectory, Is.True);
        }

        [Test]
        public void SecondNewRootGetsAllocateEtc()
        {
            EntryStore.Reset();
            var entryStore = new EntryStore();

            var rootIndex = EntryStore.NewRoot(@"C:\");
            var rootIndex2 = EntryStore.NewRoot(@"D:\");

            Assert.That(rootIndex2, Is.EqualTo(2));
            Assert.That(entryStore[rootIndex2].FullPath, Is.EqualTo(@"D:\"));
            Assert.That(entryStore[rootIndex2].Parent, Is.EqualTo(0));
            Assert.That(entryStore[rootIndex2].IsDirectory, Is.True);
        }

        //
        // THINKING
        //
        // Tree per EntryStore is much simpler.
        // Means dont have to mix/unmix a tree.
        // -- could allocate base array slots to each loaded tree.
        // -- share the base array slots
        // -- configure base array slots - :) 
        //
        // Could share but separating dealing in small bits to painful.
        // Drop the blocksize to suite this. 
        // currenlty 2^19 - 500Meg - 1 block.
        //
        // Knock it down to 2^16 = 65536 entries
        //   Entry - about 4181520 - 4Meg ish.
        //
        // in 64bit base array. -- base array start at 512, double when out.
        // -   1024 entries =  67 million entries per tree.   8KB overhead 
        // -   2048 entries = 134 million entries per tree.  16KB overhead 
        // -   8192 entries = 536 million entries per tree.  64KB overhead 
        // -  16384 entries =   1 billion entries per tree. 130KB overhead 
        // can split any tree up into pieces - but.. icky
        //


        [Test]
        public void ScanARootEntry()
        {
            EntryStore.Reset();

            long GCMemStart = 0;
            long GCMemEnd = 0;
            long ProcessMemStart = 0;
            long ProcessMemEnd = 0;

            Process objProcess = Process.GetCurrentProcess();
            GCMemStart = GC.GetTotalMemory(true);
            ProcessMemStart = objProcess.PrivateMemorySize64;

            var entryStore = new EntryStore();
            EntryStore.EntryCountThreshold = 100000;

            var rootIndex = EntryStore.NewRoot(@"D:\");
            var root = EntryStore.RootEntries.First();
            EntryStore.RecurseTree(root);
            //EntryStore.RecurseTree(root);
            //EntryStore.RecurseTree(root);

            //Assert.That(rootIndex, Is.EqualTo(1));
            //Assert.That(entryStore[rootIndex].FullPath, Is.EqualTo(@"C:\"));
            //Assert.That(entryStore[rootIndex].Parent, Is.EqualTo(0));
            //Assert.That(entryStore[rootIndex].IsDirectory, Is.True);


            Console.WriteLine(EntryStore._current);
            Console.WriteLine("0 {0}", EntryStore._block[0]);
            Console.WriteLine("1 {0}", EntryStore._block[1]);
            Console.WriteLine("2 {0}", EntryStore._block[2]);
            Console.WriteLine("3 {0}", EntryStore._block[3]);
            Console.WriteLine("4 {0}", EntryStore._block[4]);

            Thread.Sleep(500);

            //Measure memory after allocating
            GCMemEnd = GC.GetTotalMemory(true);
            ProcessMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine("GC Memory Use:{0} (bytes)", GCMemEnd - GCMemStart);
            Console.WriteLine("Process Memory Use:{0} (bytes)", ProcessMemEnd - ProcessMemStart);
            Console.WriteLine("GC Memory Start:{0} (bytes)", GCMemStart.ToString());
            Console.WriteLine("Process Memory Start:{0} (bytes)", ProcessMemStart.ToString());
            Console.WriteLine("GC Memory End:{0} (bytes)", GCMemEnd.ToString());
            Console.WriteLine("x Process Memory End:{0} (bytes)", ProcessMemEnd.ToString());
            Console.WriteLine();

        }

    }
}
