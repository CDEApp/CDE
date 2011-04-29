using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib;
using cdeLib.Infrastructure;

namespace cde
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--scan")
            {
                CreateCDECache(args[1]);
            }
            else if (args.Length == 2 && args[0] == "--find")
            {
                FindString(args[1]);
            }
            else if (args.Length ==1 && args[0] == "--md5")
            {
                CreateMd5OnCache();
            }
            else if( args.Length ==1 && args[0] == "--dupes")
            {
                FindDupes();
            }
            else
            {
                Console.WriteLine("Usage: cde --scan <path>");
                Console.WriteLine("       scans path and creates a cache file.");
                Console.WriteLine("Usage: cde --find <string>");
                Console.WriteLine("       uses all cache files available searches for <string> as substring of file system entries.");
                Console.WriteLine("Usage: cde --md5 ");
                Console.WriteLine("       Calculate md5 for all entries in cache file");
                Console.WriteLine("Usage: cde --dupes ");
                Console.WriteLine("       Show duplicates. Must of precaculated MD5 first");
            }
        }

        private static void FindDupes()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var duplication = new Duplication();
            duplication.FindDuplicates(rootEntries);
        }

        private static void CreateMd5OnCache()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var duplication = new Duplication();
            duplication.ApplyMd5Checksum(rootEntries);
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
                totalFound += rootEntry.FindEntries(find, PrintFoundEntries);
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

        private static void PrintFoundEntries(string path, DirEntry direntry)
        {
            Console.WriteLine("found {0}", path);
        }

        static void CreateCDECache(string path)
        {
            var re = new RootEntry();
            try
            {
                re.SimpleScanCountEvent = ScanEvery1000Entries;
                re.SimpleScanEndEvent = ScanEndofEntries;
                re.ExceptionEvent = PrintExceptions;

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

        private static void PrintExceptions(string path, Exception ex)
        {
            Console.WriteLine("Exception {0}, Path \"{1}\"", ex.GetType(), path);
        }

        public static void ScanEvery1000Entries()
        {
            Console.Write(".");
        }

        public static void ScanEndofEntries()
        {
            Console.WriteLine("");
        }
    }
}


// IDEA test a list of fullpaths.