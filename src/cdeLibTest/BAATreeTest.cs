using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace cdeLibTest
{
    // current direntry.. 
    // C: drive Files 149,507 Dirs 24,690 Total Size of Files 26,825,007,368
    // FILE   9,623,653 no hashes.  cde --replfind 60.3 Meg, 32 bit.
    // FILE: 12,784,234 all hashes. cde --replfind 66.0x Meg, 32bit
    //

    // in array  1,000,000 is about  32Meg.
    // in array 10,000,000 is about 320Meg.
    // a million at an array does not seem bad.
    public struct Entry
    {
        public uint DirCount;
        public uint FileCount;
        public ulong Size;
        public DateTime Modified;
        public string Name;

        // lol one bit adds a big chunk of allocated structure size, but multibits cost no more.
        //public bool IsJunction;
        //public bool IsReparse;
        public Hash16 Hash; // waste 8 bytes with pointer if we dont store it here. this is 16bytes.

        public int Child;
        public int Sibling;
        public int Parent;

        public const int IsDirectoryB = 0;
        public const int IsPartialhashB = 1;
        public int BitFields;
        #region BitFields based properties
        public bool IsDirectory
        {
            get { return (BitFields & IsDirectoryB) == IsDirectoryB; }
            set
            {
                if (value)
                {
                    BitFields |= IsDirectoryB;
                }
                else
                {
                    BitFields &= ~IsDirectoryB;
                }
            }
        }

        public bool IsPartialHash
        {
            get { return (BitFields & IsPartialhashB) == IsPartialhashB; }
            set
            {
                if (value)
                {
                    BitFields |= IsPartialhashB;
                }
                else
                {
                    BitFields &= ~IsPartialhashB;
                }
            }
        }
        #endregion
    }



    public struct Hash16
    {
        public ulong HashA; // first 8 bytes
        public ulong HashB; // last 8 bytes
    }

    public class EntryStore
    {

    }

    [TestFixture]
    class BAATreeTest
    {
        [Test]
        public void test()
        {
            long GCMemStart = 0;
            long GCMemEnd = 0;
            long ProcessMemStart = 0;
            long ProcessMemEnd = 0;

            Process objProcess = Process.GetCurrentProcess();
            GCMemStart = GC.GetTotalMemory(true);
            ProcessMemStart = objProcess.PrivateMemorySize64;


            var bigArray = 1 * 1000 * 1000; //
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

            //Measure memory after allocating
            GCMemEnd = GC.GetTotalMemory(true);
            ProcessMemEnd = objProcess.PrivateMemorySize64;

            Console.WriteLine("GC Memory Use:{0} (bytes)", GCMemEnd - GCMemStart);
            Console.WriteLine("Process Memory Use:{0} (bytes)", ProcessMemEnd - ProcessMemStart);
            Console.WriteLine("GC Memory Start:{0} (bytes)", GCMemStart.ToString());
            Console.WriteLine("Process Memory Start:{0} (bytes)", ProcessMemStart.ToString());
            Console.WriteLine("GC Memory End:{0} (bytes)", GCMemEnd.ToString());
            Console.WriteLine("Process Memory End:{0} (bytes)", ProcessMemEnd.ToString());
            Console.WriteLine();
            Console.ReadLine();
        }
    }
}
