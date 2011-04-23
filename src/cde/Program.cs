using System;
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
                CreateCDECache(@"D:\");
                CreateCDECache(@"E:\");
                CreateCDECache(@"F:\");
                CreateCDECache(@"G:\");
                //Console.Write("Press return to continue");
                //Console.Out.Flush();
                //var name = Console.ReadLine();
            }
            else
            {
                FindString(args[0]);
            }
        }

        static void FindString(string find)
        {
            var start = DateTime.UtcNow;
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var end = DateTime.UtcNow;
            var loadTimeSpan = end - start;
            Console.WriteLine("Loaded {0} file(s) in {1} msecs", rootEntries.Count, loadTimeSpan.Milliseconds);
            foreach (var rootEntry in rootEntries)
            {
                Console.WriteLine("Loaded File {0} with {1} entries.", rootEntry.DefaultFileName, rootEntry.DirCount + rootEntry.FileCount);
            }
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.FindEntries(find);
            }
            Console.WriteLine("Done.");
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