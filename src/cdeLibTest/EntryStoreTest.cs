using System;
using System.Diagnostics;
using System.Threading;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeLibTest;

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
