using System;
using System.Diagnostics;
using System.Threading;
using cdeLib;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    internal class EntrySizeOfStructTest
    {
        [Ignore("Ignore me")]
        [Test]
        public void Test()
        {
            var objProcess = Process.GetCurrentProcess();
            var gcMemStart = GC.GetTotalMemory(true);
            var processMemStart = objProcess.PrivateMemorySize64;

            const int bigArray = 65536;
            var store = new Entry[bigArray];
            //var store2 = new Entry[bigArray];
            //var store3 = new Entry[bigArray];
            //var store4 = new Entry[bigArray];

            for (var i = 0; i < bigArray; i++)
            {
                store[i].Size = (ulong)i;
                //store2[i].Size = (ulong)i;
                //store3[i].Size = (ulong)i;
                //store4[i].Size = (ulong)i;
            }
            Thread.Sleep(500);

            //Measure memory after allocating
            var gcMemEnd = GC.GetTotalMemory(true);
            var processMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine($"GC Memory Use:{gcMemEnd - gcMemStart} (bytes)");
            Console.WriteLine($"Process Memory Use:{processMemEnd - processMemStart} (bytes)");
            Console.WriteLine($"GC Memory Start:{gcMemStart} (bytes)");
            Console.WriteLine($"Process Memory Start:{processMemStart} (bytes)");
            Console.WriteLine($"GC Memory End:{gcMemEnd} (bytes)");
            Console.WriteLine($"x Process Memory End:{processMemEnd} (bytes)");
            Console.WriteLine();
            //Console.ReadLine();
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    internal class EntryStoreTest_Root_Scan
    {
        //     [Test]
        //     public void SetRoot_WithActualDrive_RetrieveSetsExpectedData()
        //     {
        //         var e = new EntryStore();
        //
        //         var rootIndex = e.SetRoot(@"C:\");
        //
        //         Assert.That(rootIndex, Is.EqualTo(1));
        //         Entry[] block;
        //         var entryIndex = e.EntryIndex(rootIndex, out block);
        //         Assert.That(block[entryIndex].FullPath, Is.EqualTo(@"C:\"));
        //         Assert.That(block[entryIndex].Parent, Is.EqualTo(0));
        //         Assert.That(block[entryIndex].IsDirectory, Is.True);
        //     }
        //
        //     [Test]
        //     public void RecurseTree_WithRootNotSet_ThrowsException()
        //     {
        //         var e = new EntryStore();
        //
        //         Assert.Throws<ArgumentException>(()=>e.RecurseTree());
        //     }
        //
        //     [Test]
        //     public void SaveToFile_WithRootNotSet_ThrowsException()
        //     {
        //         var e = new EntryStore();
        //         Assert.Throws<Exception>(() => e.SaveToFile());
        //     }
        //
        //     [Test]
        //     public void SaveToFile_WithMinimal_EntryStore_WithARoot_OK()
        //     {
        //         var e = new EntryStore();
        //         var myRootIndex = e.AddEntry(null, @"X:\", 55,
        //             new DateTime(2011, 05, 04, 10, 09, 08), true);
        //
        //         e.Root = new RootEntry
        //         {
        //             RootIndex = myRootIndex,
        //             DefaultFileName = "test1.cdx",
        //             FullPath = @"X:\",
        //         };
        //
        //         e.SaveToFile();
        //     }
        //
        //     [Test]
        //     public void SaveToFile_WithRootAndOneEntry_EntryStore_OK()
        //     {
        //         var e = new EntryStore();
        //         var myRootIndex = e.AddEntry(null, @"X:\", 55, 
        //             new DateTime(2011, 05, 04, 10, 09, 08), true);
        //
        //         e.Root = new RootEntry
        //         {
        //             RootIndex = myRootIndex,
        //             DefaultFileName = "test2.cdx",
        //             FullPath = @"X:\",
        //         };
        //
        //         e.AddEntry("file1", null, 55, 
        //             new DateTime(2011, 05, 04, 10, 09, 08), parentIndex: myRootIndex);
        //
        //         e.SaveToFile();
        //     }
        //
        //     [Test]
        //     public void SaveToFile_WithActualDriveConfigured_NoContentScan_RetrieveSetsExpectedData()
        //     {
        //         var e = new EntryStore();
        //
        //         e.SetRoot(@"C:\");
        //
        //         e.SaveToFile();
        //     }
        //
        //     //
        //     // THINKING
        //     //
        //     // Tree per EntryStore is much simpler.
        //     // Means dont have to mix/unmix a tree.
        //     // -- could allocate base array slots to each loaded tree.
        //     // -- share the base array slots
        //     // -- configure base array slots - :) 
        //     //
        //     // Could share but separating dealing in small bits to painful.
        //     // Drop the blocksize to suite this. 
        //     // currenlty 2^19 - 500Meg - 1 block.
        //     //
        //     // Knock it down to 2^16 = 65536 entries
        //     //   Entry - about 4181520 - 4Meg ish.
        //     //
        //     // in 64bit base array. -- base array start at 512, double when out.
        //     // -   1024 entries =  67 million entries per tree.   8KB overhead 
        //     // -   2048 entries = 134 million entries per tree.  16KB overhead 
        //     // -   8192 entries = 536 million entries per tree.  64KB overhead 
        //     // -  16384 entries =   1 billion entries per tree. 130KB overhead 
        //     // can split any tree up into pieces - but.. icky
        //     //
        //     [Ignore("Depends on a cde file in test folder.")]
        //     [Test]
        //     public void Read_ASavedFile_OK()
        //     {
        //         var e2 = EntryStore.Read("C-Vertex3.cde");
        //         Console.WriteLine($"loaded {e2.Root.DefaultFileName}");
        //     }
        //
        //     [Ignore("its a long test available for convenience.")]
        //     [Test]
        //     public void RecurseTree_ScanCDrive_OK()
        //     {
        //         var e = new EntryStore();
        //
        //         var objProcess = Process.GetCurrentProcess();
        //         var gcMemStart = GC.GetTotalMemory(true);
        //         var processMemStart = objProcess.PrivateMemorySize64;
        //
        //         e.EntryCountThreshold = 100000;
        //
        //         //var rootIndex = 
        //         e.SetRoot(@"C:\");
        //         e.RecurseTree();
        //         e.SaveToFile();
        //
        //         Console.WriteLine($"BaseBlockCount {e.BaseBlockCount}");
        //
        //         Console.WriteLine($@"after scanning C:\ NextAvailableIndex = {e.NextAvailableIndex}");
        //
        //         uint nonEmptyNameCount = 0;
        //         uint nonEmptyFullPathCount = 0;
        //
        //         for (var i = 0; i < e.NextAvailableIndex; i++) // this iterates over all entries LOL!!
        //         {
        //             Entry[] block;
        //             var eIndex = e.EntryIndex(i, out block);
        //             if (!string.IsNullOrEmpty(block[eIndex].Name))
        //             {
        //                 ++nonEmptyNameCount;
        //             }
        //             if (!string.IsNullOrEmpty(block[eIndex].FullPath))
        //             {
        //                 ++nonEmptyFullPathCount;
        //             }
        //         }
        //
        //         Console.WriteLine($@"NonEmptyNameCount = {nonEmptyNameCount}");
        //         Console.WriteLine($@"NonEmptyFullPathCount = {nonEmptyFullPathCount}");
        //
        //         //Measure memory after allocating -- this isnt working in Release run ? to quick ?
        //         var gcMemEnd = GC.GetTotalMemory(true);
        //         var processMemEnd = objProcess.PrivateMemorySize64;
        //
        //         Console.WriteLine($"GC Memory Use:{gcMemEnd - gcMemStart} (bytes)");
        //         Console.WriteLine($"Process Memory Use:{processMemEnd - processMemStart} (bytes)");
        //         Console.WriteLine($"GC Memory Start:{gcMemStart} (bytes)");
        //         Console.WriteLine($"Process Memory Start:{processMemStart} (bytes)");
        //         Console.WriteLine($"GC Memory End:{gcMemEnd} (bytes)");
        //         Console.WriteLine($"x Process Memory End:{processMemEnd} (bytes)");
        //         Console.WriteLine();
        //         File.Delete(e.Root.DefaultFileName); // get rid of file we created.
        //     }
        // }
        // // ReSharper restore InconsistentNaming
        //
        // [TestFixture]
        // class EntryStoreBlockAllocation
        // {
        //     // ReSharper disable InconsistentNaming
        //     [Test]
        //     public void Construtor_Minimal_OK()
        //     {
        //         var e = new EntryStore();
        //
        //         Assert.That(e, Is.Not.Null);
        //     }
        //
        //     [Test]
        //     public void EntryIndex_FirstTime_ZeroethBlock_UnallocatedThrowsException()
        //     {
        //         var e = new EntryStore();
        //         Entry[] blockIndex;
        //
        //         Assert.Throws<IndexOutOfRangeException>(() => e.EntryIndex(1, out blockIndex));
        //     }
        //
        //     [Test]
        //     public void EntryIndex_FirstTime_ZeroethBlock()
        //     {
        //         var e = new EntryStore();
        //         Entry[] block;
        //
        //         var newIndex = e.AddEntry();
        //         var entryIndex = e.EntryIndex(1, out block);
        //
        //         Assert.That(newIndex, Is.EqualTo(1));
        //         Assert.That(entryIndex, Is.EqualTo(1));
        //         Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        //     }
        //
        //     [Test]
        //     public void EntryIndex_65537_FirstBlock()
        //     {
        //         var e = new EntryStore();
        //
        //         for (var i = 0; i < 65537; i++)
        //         {
        //             e.AddEntry();
        //         }
        //         Entry[] block;
        //         var entryIndex = e.EntryIndex(65537, out block);
        //
        //         Assert.That(entryIndex, Is.EqualTo(1));
        //         Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        //     }
        //
        //     [Test]
        //     public void EntryIndex_MaxIndex_OK()
        //     {
        //         const int maxIndex = 65536 * 256 - 1;
        //         var e = new EntryStore();
        //         for (var i = 0; i < maxIndex; i++)
        //         {
        //             e.AddEntry();
        //         }
        //         Entry[] block;
        //         var entryIndex = e.EntryIndex(maxIndex, out block);
        //
        //         Assert.That(entryIndex, Is.EqualTo(65535));
        //         Assert.That(block[entryIndex], Is.TypeOf(typeof(Entry)));
        //     }
        //     // ReSharper restore InconsistentNaming
    }
}

