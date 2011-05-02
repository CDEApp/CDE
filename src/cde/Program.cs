﻿using System;
using System.Diagnostics;
using cdeLib;

namespace cde
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length ==0)
            {
                ShowHelp();
                return;
            }
            var param0 = args[0].ToLowerInvariant();
            if (args.Length == 2 && param0 == "--scan")
            {
                CreateCDECache(args[1]);
            }
            else if (args.Length == 2 && Find.FindParams.Contains(param0))
            {
                Find.FindString(args[1], param0);
            }
            else if (args.Length ==1 && args[0] == "--hash")
            {
                CreateMd5OnCache();
            }
            else if( args.Length ==1 && args[0] == "--dupes")
            {
                FindDupes();
            }
            else
            {
                ShowHelp();
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: cde --scan <path>");
            Console.WriteLine("       scans path and creates a cache file.");
            Console.WriteLine("       copies hashes from old cache file to new one if old found.");
            Console.WriteLine("Usage: cde --find <string>");
            Console.WriteLine("       uses all cache files available searches for <string>");
            Console.WriteLine("       as substring of on file name.");
            Console.WriteLine("Usage: cde --grep <regex>");
            Console.WriteLine("       uses all cache files available searches for <regex>");
            Console.WriteLine("       as regex match on file name.");
            Console.WriteLine("Usage: cde --greppath <regex>");
            Console.WriteLine("       uses all cache files available searches for <regex>");
            Console.WriteLine("        as regex match on full path to file name.");
            Console.WriteLine("Usage: cde --hash ");
            Console.WriteLine("       Calculate hash (MD5) for all entries in cache file");
            Console.WriteLine("Usage: cde --dupes ");
            Console.WriteLine("       Show duplicates. Must of already run --hash first to compute file hashes");
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
            Stopwatch sw = new Stopwatch();
            sw.Start();
            duplication.ApplyMd5Checksum(rootEntries);
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            Console.WriteLine("Hash took : {0}",elapsedTime);
        }

        static void CreateCDECache(string path)
        {
            var re = new RootEntry();
            try
            {
                re.SimpleScanCountEvent = ScanCountPrintDot;
                re.SimpleScanEndEvent = ScanEndofEntries;
                re.ExceptionEvent = PrintExceptions;

                re.PopulateRoot(path);

                var oldRoot = RootEntry.LoadDirCache(re.DefaultFileName);
                if (oldRoot != null)
                {
                    Console.WriteLine("Found cache \"{0}\"", re.DefaultFileName);
                    Console.WriteLine("Updating hashs on new scan from found cache file.");
                    oldRoot.TraverseTreesCopyHash(re);
                }

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

        public static void ScanCountPrintDot()
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