using System;
using System.Diagnostics;
using System.Reflection;
using cdeLib;

namespace cde
{
    class Program
    {
        public static string Version
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                return String.Format("{0} v{1}.{2}", fvi.ProductName,
                    fvi.ProductMajorPart, fvi.ProductMinorPart);
            }
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += BreakConsole;
            if (args.Length ==0)
            {
                ShowHelp();
                return;
            }
            var param0 = args[0].ToLowerInvariant();
            if (args.Length == 2 && param0 == "--scan")
            {
                CreateCache(args[1]);
            }
            else if (args.Length == 2 && Find.FindParams.Contains(param0))
            {
                Find.FindString(args[1], param0);
            }
            else if (args.Length == 2 && param0 == "--replgreppath")
            {
                FindRepl(Find.ParamGreppath, args[1]);
            }
            else if (args.Length == 2 && param0 == "--replgrep")
            {
                FindRepl(Find.ParamGrep, args[1]);
            }
            else if (args.Length == 2 && param0 == "--replfind")
            {
                FindRepl(Find.ParamFind, args[1]);
            }
            else if (args.Length == 1 && param0 == "--hash")
            {
                CreateMd5OnCache();
            }
            else if (args.Length == 1 && param0 == "--dupes")
            {
                FindDupes();
            }
            else if (args.Length == 1 && param0 == "--treedump1")
            {
                PrintPathsHaveHash();
            }
            else if (args.Length == 1 && param0 == "--treedump2")
            {
                PrintPathsHaveHashEnumerator();
            }
            else if (args.Length == 1 && param0 == "--version")
            {
                Console.WriteLine(Version);
            }
            else
            {
                ShowHelp();
            }
        }

        private static void BreakConsole(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("\n * Break key detected. will exit as soon as current file process is completed.");
            Hack.BreakConsoleFlag = true;
            e.Cancel = true;
        }

        // repl = read-eval-print-loop
        private static void FindRepl(string parmString, string firstPattern)
        {
            Find.GetDirCache();
            Find.FindString(firstPattern, parmString);
            do
            {
                if (Hack.BreakConsoleFlag) 
                    Hack.BreakConsoleFlag = false; //reset otherwise we'll get some weird behavouir in loop.
                Console.Write("Enter string to search <nothing exits>: ");
                var pattern = Console.ReadLine();
                if (pattern == string.Empty)
                {
                    Console.WriteLine("Exiting...");
                    break;
                }
                Find.FindString(pattern, parmString);
            } while (true);
        }

        private static void ShowHelp()
        {
            Console.WriteLine(Version);
            Console.WriteLine("Usage: cde --version");
            Console.WriteLine("       display version.");
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
            Console.WriteLine("       as regex match on full path to file name.");
            Console.WriteLine("Usage: cde --hash ");
            Console.WriteLine("       Calculate hash (MD5) for all entries in cache file");
            Console.WriteLine("Usage: cde --dupes ");
            Console.WriteLine("       Show duplicates. Must of already run --hash first to compute file hashes");
            Console.WriteLine("Usage: cde --replGreppath <regex>");
            Console.WriteLine("Usage: cde --replGrep <regex>");
            Console.WriteLine("Usage: cde --repFind <regex>");
            Console.WriteLine("       read-eval-print loops version of the 3 find options.");
            Console.WriteLine("       This one is repl it doesnt exit unless you press enter with no search term.");
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
            var sw = new Stopwatch();
            sw.Start();
            duplication.ApplyMd5Checksum(rootEntries);
            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            Console.WriteLine("Hash took : {0}",elapsedTime);
        }

        static void CreateCache(string path)
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

        private static void PrintPathsHaveHash()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            CommonEntry.TraverseAllTrees(rootEntries, PrintPathWithHash);
        }

        private static void PrintPathWithHash(string filePath, DirEntry de)
        {
            var hash = de.Hash == null ? " " : "#";
            Console.WriteLine("{0}{1}", hash, filePath);
        }

        private static void PrintPathsHaveHashEnumerator()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var deEnumerator = CommonEntry.GetFDEs(rootEntries);
            foreach (var fde in deEnumerator)
            {
                var hash = fde.DirEntry.Hash == null ? " " : "#";
                Console.WriteLine("{0}{1}", hash, fde.FilePath);
            }
        }
    }
}


// IDEA test a list of fullpaths.