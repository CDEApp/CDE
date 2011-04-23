using System;
using cdeLib;

namespace cde
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "/cache")
            {
                CreateCDECache(args[1]);
            }
            //if (args.Length == 0)
            //{
            //    CreateCDECache(@"C:\");
            //    CreateCDECache(@"D:\");
            //    CreateCDECache(@"E:\");
            //    CreateCDECache(@"F:\");
            //    CreateCDECache(@"G:\");
            //    //Console.Write("Press return to continue");
            //    //Console.Out.Flush();
            //    //var name = Console.ReadLine();
            //}
            else if (args.Length == 1)
            {
                FindString(args[0]);
            }
            else
            {
                Console.WriteLine("Usage: cde /cache <path>");
                Console.WriteLine("       creates a cache file of path in current directory");
                Console.WriteLine("Usage: cde <string>");
                Console.WriteLine("       uses all cache files in current directory and searches for <string> as substring of file system entries.");
            }
        }

        static void FindString(string find)
        {
            Console.WriteLine("Searching for entries that contain \"{0}\"", find);
            var start = DateTime.UtcNow;
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var end = DateTime.UtcNow;
            var loadTimeSpan = end - start;
            Console.WriteLine("Loaded {0} file(s) in {1:0.00} msecs", rootEntries.Count, loadTimeSpan.TotalMilliseconds);
            foreach (var rootEntry in rootEntries)
            {
                Console.WriteLine("Loaded File {0} with {1} entries.", rootEntry.DefaultFileName, rootEntry.DirCount + rootEntry.FileCount);
            }
            var totalFound = 0u;
            foreach (var rootEntry in rootEntries)
            {
                totalFound += rootEntry.FindEntries(find);
            }
            if (totalFound > 0)
            {
                Console.WriteLine("Found a total of {0} entries. Containing the string \"{1}\"", totalFound, find);
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

        static void CreateCDECache(string path)
        {
            var re = new RootEntry();
            try
            {
                re.PopulateRoot(path);
                re.SaveRootEntry();
                var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
                Console.WriteLine("Scanned Path {0}", re.RootPath);
                Console.WriteLine("Scan time {0:0.00} msecs", scanTimeSpan.TotalMilliseconds);
                Console.WriteLine("Saved Scanned Path {0}", re.DefaultFileName);
                Console.WriteLine("Files {0:0,0} Dirs {1:0,0} Total Size of Files {2:0,0}", re.FileCount, re.DirCount, re.Size);
            }
            catch (ArgumentException aex)
            {
                Console.WriteLine("Error: {0}", aex.Message);
            }

        }
    }
}


// IDEA test a list of fullpaths.