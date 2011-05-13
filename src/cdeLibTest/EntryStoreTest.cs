using System;
using System.Diagnostics;
using System.Threading;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class EntrySizeOfStructTest
    {
        [Test]
        public void Test()
        {
            var objProcess = Process.GetCurrentProcess();
            long gcMemStart = GC.GetTotalMemory(true);
            long processMemStart = objProcess.PrivateMemorySize64;

            const int bigArray = 65536;
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
            long gcMemEnd = GC.GetTotalMemory(true);
            long processMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine("GC Memory Use:{0} (bytes)", gcMemEnd - gcMemStart);
            Console.WriteLine("Process Memory Use:{0} (bytes)", processMemEnd - processMemStart);
            Console.WriteLine("GC Memory Start:{0} (bytes)", gcMemStart);
            Console.WriteLine("Process Memory Start:{0} (bytes)", processMemStart);
            Console.WriteLine("GC Memory End:{0} (bytes)", gcMemEnd);
            Console.WriteLine("x Process Memory End:{0} (bytes)", processMemEnd);
            Console.WriteLine();
            //Console.ReadLine();
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    class EntryStoreTest_Root_Scan
    {
        [Test]
        public void SetRoot_WithActualDrive_RetrieveSetsExpectedData()
        {
            var e = new EntryStore();

            var rootIndex = e.SetRoot(@"C:\");

            Assert.That(rootIndex, Is.EqualTo(1));
            Entry[] block;
            var entryIndex = e.EntryIndex(rootIndex, out block);
            Assert.That(block[entryIndex].FullPath, Is.EqualTo(@"C:\"));
            Assert.That(block[entryIndex].Parent, Is.EqualTo(0));
            Assert.That(block[entryIndex].IsDirectory, Is.True);
        }

        [ExpectedException(typeof(ArgumentException))]
        [Test]
        public void RecurseTree_WithRootNotSet_ThrowsException()
        {
            var e = new EntryStore();

            e.RecurseTree();
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
        public void RecurseTree_ScanCDrive_OK()
        {
            var e = new EntryStore();

            Process objProcess = Process.GetCurrentProcess();
            long gcMemStart = GC.GetTotalMemory(true);
            long processMemStart = objProcess.PrivateMemorySize64;

            e.EntryCountThreshold = 100000;

            //var rootIndex = 
            e.SetRoot(@"C:\");
            e.RecurseTree();

            Console.WriteLine("0 {0}", e.BaseBlock[0]);
            Console.WriteLine("1 {0}", e.BaseBlock[1]);
            Console.WriteLine("2 {0}", e.BaseBlock[2]);
            Console.WriteLine("3 {0}", e.BaseBlock[3]);
            Console.WriteLine("4 {0}", e.BaseBlock[4]);
            //Console.WriteLine("5 {0}", e.BaseBlock[5]);
            //Console.WriteLine("6 {0}", e.BaseBlock[6]);
            //Console.WriteLine("7 {0}", e.BaseBlock[7]);
            //Console.WriteLine("8 {0}", e.BaseBlock[8]);
            //Console.WriteLine("9 {0}", e.BaseBlock[9]);

            Console.WriteLine(@"after scanning C:\ NextAvailableIndex = {0}", e.NextAvailableIndex);

            uint nonEmptyNameCount = 0;
            uint nonEmptyFullPathCount = 0;

            for (uint i = 0; i < e.NextAvailableIndex; i++) // this iterates over all entries LOL!!
            {
                Entry[] block;
                uint eIndex = e.EntryIndex(i, out block);
                if (!string.IsNullOrEmpty(block[eIndex].Name))
                {
                    ++nonEmptyNameCount;
                }
                if (!string.IsNullOrEmpty(block[eIndex].FullPath))
                {
                    ++nonEmptyFullPathCount;
                }
            }

            Console.WriteLine(@"NonEmptyNameCount = {0}", nonEmptyNameCount);
            Console.WriteLine(@"NonEmptyFullPathCount = {0}", nonEmptyFullPathCount);

            //Measure memory after allocating -- this isnt working in Release run ? to quick ?
            long gcMemEnd = GC.GetTotalMemory(true);
            long processMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine("GC Memory Use:{0} (bytes)", gcMemEnd - gcMemStart);
            Console.WriteLine("Process Memory Use:{0} (bytes)", processMemEnd - processMemStart);
            Console.WriteLine("GC Memory Start:{0} (bytes)", gcMemStart);
            Console.WriteLine("Process Memory Start:{0} (bytes)", processMemStart);
            Console.WriteLine("GC Memory End:{0} (bytes)", gcMemEnd);
            Console.WriteLine("x Process Memory End:{0} (bytes)", processMemEnd);
            Console.WriteLine();
        }
    }
    // ReSharper restore InconsistentNaming

    [TestFixture]
    class EntryStoreBlockAllocation
    {
        // ReSharper disable InconsistentNaming
        [Test]
        public void Construtor_Minimal_OK()
        {
            var e = new EntryStore();

            Assert.That(e, Is.Not.Null);
        }

        [ExpectedException(typeof(IndexOutOfRangeException))]
        [Test]
        public void EntryIndex_FirstTime_ZeroethBlock_UnallocatedThrowsException()
        {
            var e = new EntryStore();
            Entry[] blockIndex;

            e.EntryIndex(1u, out blockIndex);
        }

        [Test]
        public void EntryIndex_FirstTime_ZeroethBlock()
        {
            var e = new EntryStore();
            Entry[] block;

            var newIndex = e.AddEntry();
            var entryIndex = e.EntryIndex(1u, out block);

            Assert.That(newIndex, Is.EqualTo(1));
            Assert.That(entryIndex, Is.EqualTo(1));
            Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        }

        [Test]
        public void EntryIndex_65537_FirstBlock()
        {
            var e = new EntryStore();

            for (int i = 0; i < 65537; i++)
            {
                e.AddEntry();
            }
            Entry[] block;
            var entryIndex = e.EntryIndex(65537u, out block);

            Assert.That(entryIndex, Is.EqualTo(1));
            Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        }

        [Test]
        public void EntryIndex_MaxIndex_OK()
        {
            const uint maxIndex = 65536 * 256u - 1;
            var e = new EntryStore();
            for (int i = 0; i < maxIndex; i++)
            {
                e.AddEntry();
            }
            Entry[] block;
            var entryIndex = e.EntryIndex(maxIndex, out block);

            Assert.That(entryIndex, Is.EqualTo(65535));
            Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        }

        [ExpectedException(typeof(IndexOutOfRangeException))]
        [Test]
        public void AddEntry_MaxIndexPlus1_Exception()
        {
            const uint maxIndexPlus1 = 65536 * 256u ;
            var e = new EntryStore();
            for (int i = 0; i < maxIndexPlus1; i++)
            {
                e.AddEntry();
            }
        }


        [Ignore("The indexer is useless with struct, so use EntryIndex with out Entry[]")]
        [Test]
        public void Indexer_LooksupAddedData_OK()
        {
            var e = new EntryStore();
            var myNewIndex = e.AddEntry();


            Entry[] block;
            var entryIndex = e.EntryIndex(myNewIndex, out block);
            block[entryIndex].Name = "Fred";

            Console.WriteLine("  grrr {0} {1} {2}", myNewIndex, block[entryIndex].Name, block[entryIndex].Size);

            var myNewIndex2 = e.AddEntry();

            var blockX = (Entry[])e.BaseBlock[0];
            blockX[myNewIndex2].Name = "moo";

            Console.WriteLine("  grrr {0} {1} {2}", myNewIndex, block[myNewIndex2].Name, block[myNewIndex2].Size);

            //var s1 = e[myNewIndex2];

            //var x = e[myNewIndex2]; // this doesnt work it copies the struct out.
            //x.Name = "a";

            //var s2 = e[myNewIndex2];
            //Console.WriteLine("  grrr {0} {1} {2}", x, s1, s2);
        }


        // ReSharper restore InconsistentNaming
    }
}

