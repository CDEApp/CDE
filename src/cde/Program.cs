using System;
using System.IO;
using cdeLib;

namespace cde
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                CreateCDECache(@"C:\");
                CreateCDECache(@"E:\");
                CreateCDECache(@"F:\");
                CreateCDECache(@"G:\");

                Console.Write("Press return to continue");
                Console.Out.Flush();
                var name = Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Finding " + args[0]);
                FindString(@"c_SM15T_1_3.cde", args[0]);
            }
        }

        static void FindString(string cacheFile, string find)
        {
            DateTime start = DateTime.UtcNow;
            var newFS = new FileStream(cacheFile, FileMode.Open);
            RootEntry rootEntry = RootEntry.Read(newFS);
            DateTime end = DateTime.UtcNow;
            var loadTimeSpan = end - start;
            Console.WriteLine("Loaded Done... {0} in {1}msecs", cacheFile, loadTimeSpan.Milliseconds);
            Console.WriteLine("Loaded Entries... {0}", rootEntry.DirCount + rootEntry.FileCount);

            rootEntry.FindEntries(find);
            
            Console.WriteLine("Done. ");

        }

        static void CreateCDECache(string path)
        {
            var re = new RootEntry();
            re.PopulateRoot(path);
            re.SaveRootEntry();
            var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
            Console.WriteLine("Scanned Path {0}", re.RootPath);
            Console.WriteLine("Scan time {0:0.00} msecs", scanTimeSpan.TotalMilliseconds);
            Console.WriteLine("Saved Scanned Path {0}", re.DefaultFileName);
            Console.WriteLine("Files {0:0,0} Dirs {1:0,0} Total Size of Files {2:0,0}", re.FileCount, re.DirCount, re.Size);
        }
    }
}


// IDEA test a list of fullpaths.