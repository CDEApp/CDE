using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autofac;
using cdeLib;
using cdeLib.Infrastructure;

namespace cde
{
    static class Program
    {
        public static IContainer Container;
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
            Container = BootStrapper.Components();
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
            else if (args.Length == 2 && param0 == "--scan2")
            {
                CreateCache2(args[1]);
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
            else if (args.Length == 1 && param0 == "--hash2")
            {
                CreateMd5OnCache2();
            }
            else if (args.Length == 1 && param0 == "--dupes")
            {
                FindDupes();
            }
            else if (args.Length == 1 && param0 == "--treedump1")
            {
                PrintPathsHaveHashEnumerator();
            }
            else if (args.Length == 1 && param0 == "--treedump2")
            {
                EntryStore.PrintPathsHaveHash();
            }
            else if (args.Length == 1 && param0 == "--version")
            {
                Console.WriteLine(Version);
            }
            else if (args.Length == 1 && param0 == "--loadwait")
            {
                Console.WriteLine(Version);
                RootEntry.LoadCurrentDirCache();
                Console.ReadLine();
            }
            else if (args.Length == 1 && param0 == "--loadwait2")
            {
                Console.WriteLine(Version);
                EntryStore.LoadCurrentDirCache();
                Console.ReadLine();
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
            var duplication = Container.Resolve<Duplication>();
            duplication.FindDuplicates(rootEntries);
        }

        private static void CreateMd5OnCache()
        {
            var logger = Container.Resolve<ILogger>();
            var diagnostics = Container.Resolve<IApplicationDiagnostics>();
            logger.LogInfo(String.Format("Memory pre-catload: {0}",diagnostics.GetMemoryAllocated().FormatAsBytes()));
            var rootEntries = RootEntry.LoadCurrentDirCache();
            logger.LogInfo(String.Format("Memory post-catload: {0}", diagnostics.GetMemoryAllocated().FormatAsBytes()));
            var duplication = Container.Resolve<Duplication>();
            var sw = new Stopwatch();
            sw.Start();
            
            duplication.ApplyMd5Checksum(rootEntries);
            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            Console.WriteLine("Hash took : {0}",elapsedTime);
        }

        private static void CreateMd5OnCache2()
        {
            var rootEntries = EntryStore.LoadCurrentDirCache();
            var re = rootEntries.FirstOrDefault();
            if (re != null)
            {
                re.ApplyMd5Checksum();
            }
        }


        static void CreateCache2(string path)
        {
            //Process objProcess = Process.GetCurrentProcess();
            //long gcMemStart = GC.GetTotalMemory(true);
            //long processMemStart = objProcess.PrivateMemorySize64;

            var e = new EntryStore();
            e.SimpleScanCountEvent = ScanCountPrintDot;
            e.SimpleScanEndEvent = ScanEndofEntries;
            e.EntryCountThreshold = 10000;

            e.SetRoot(path);
            e.Root.ScanStartUTC = DateTime.UtcNow;
            e.RecurseTree();
            e.Root.ScanEndUTC = DateTime.UtcNow;
            e.SaveToFile();
            var scanTimeSpan = (e.Root.ScanEndUTC - e.Root.ScanStartUTC);
            Console.WriteLine("Scanned Path {0}", e.Root.RootPath);
            Console.WriteLine("Scan time {0:0.00} msecs", scanTimeSpan.TotalMilliseconds);
            Console.WriteLine("Saved Scanned Path {0}", e.Root.DefaultFileName);
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

        private static void ScanCountPrintDot()
        {
            Console.Write(".");
        }

        private static void ScanEndofEntries()
        {
            Console.WriteLine("");
        }

        private static void PrintPathsHaveHashEnumerator()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var pdee = CommonEntry.GetPairDirEntries(rootEntries);
            foreach (var pairDirEntry in pdee)      
            {
                var hash = pairDirEntry.ChildDE.Hash == null ? " " : "#";
                var fullPath = CommonEntry.MakeFullPath(pairDirEntry.ParentDE, pairDirEntry.ChildDE);
                Console.WriteLine("{0}{1}", hash, fullPath);                           
            }
        }
    }
}
