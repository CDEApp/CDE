using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autofac;
using cdeLib;
using cdeLib.Infrastructure;

using Mono.Terminal;

namespace cde
{
    public static class Program
    {
        public static IContainer Container;
        public static string Version
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                return $"{fvi.ProductName} v{fvi.ProductMajorPart}.{fvi.ProductMinorPart}";
            }
        }

        private static void Main(string[] args)
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
                Find.StaticFind(args[1], param0);
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
            else if (args.Length == 1 && param0 == "--repl")
            {
                var le = new LineEditor(null);
                string s;
                var running = true;

                while (running && (s = le.Edit("shell> ", string.Empty)) != null)
                {
                    Console.WriteLine("----> [{0}]", s);
                    switch (s)
                    {
                        case "quit":
                            running = false;
                            break;
                        case "history":
                        case "!":
                            le.CmdHistoryDump();
                            break;
                        case "help":
                        case "?":
                            Console.WriteLine("Builtin Commands:");
                            Console.WriteLine("  quit - quit, ");
                            Console.WriteLine("  help - show help, ? - show help");
                            Console.WriteLine("  history - show history, ! - show history");
                            Console.WriteLine("Keystrokes:");
                            Console.WriteLine("  Home, End, Left, Right,  Up, Down, Back, Del, Tab");
                            Console.WriteLine("  C-a,  C-e,  C-b,   C-f, C-p,  C-n,       C-d");
                            Console.WriteLine("  C-l - clear console to top");
                            Console.WriteLine("  C-r - reverse seach history");
                            Console.WriteLine("  A-b - move backward word");
                            Console.WriteLine("  A-f - move forward word");
                            Console.WriteLine("  A-d - delete word forward");
                            Console.WriteLine("  A-Backspace - delete word backward");
                            break;
                    }
                }
            }
            else if (args.Length == 2 && param0 == "--populousfolders")
            {
                int count;
                if (int.TryParse(args[1], out count))
                {
                   FindPouplous(count);
                }
                else
                {
                    Console.WriteLine("Populous folders option requires an integer as second parameter");
                }
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
            Find.StaticFind(firstPattern, parmString);
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
                Find.StaticFind(pattern, parmString);
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
            Console.WriteLine("Usage: cde --findpath <string>");
            Console.WriteLine("       uses all cache files available searches for <string>");
            Console.WriteLine("       as regex match on full path to file name.");
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
            Console.WriteLine("Usage: cde --repl");
            Console.WriteLine("       Enter readline mode - trying it out not useful yet...");
            Console.WriteLine("Usage: cde --replGreppath <regex>");
            Console.WriteLine("Usage: cde --replGrep <regex>");
            Console.WriteLine("Usage: cde --replFind <regex>");
            Console.WriteLine("       read-eval-print loops version of the 3 find options.");
            Console.WriteLine("       This one is repl it doesnt exit unless you press enter with no search term.");
            Console.WriteLine("Usage: cde --populousfolders <minimumcount>");
            Console.WriteLine("       output folders containing more than <minimumentrysize> entires.");
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
            logger.LogInfo("Memory pre-catload: {0}",diagnostics.GetMemoryAllocated().FormatAsBytes());
            var rootEntries = RootEntry.LoadCurrentDirCache();
            logger.LogInfo("Memory post-catload: {0}", diagnostics.GetMemoryAllocated().FormatAsBytes());
            var duplication = Container.Resolve<Duplication>();
            var sw = new Stopwatch();
            sw.Start();
            
            duplication.ApplyMd5Checksum(rootEntries);

            foreach (var rootEntry in rootEntries)
            {
                logger.LogDebug("Saving {0}", rootEntry.DefaultFileName);
                rootEntry.SaveRootEntry();
            }

            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",ts.Hours, ts.Minutes, ts.Seconds,ts.Milliseconds / 10);
            Console.WriteLine("Hash took : {0}",elapsedTime);
        }

        private static void CreateMd5OnCache2()
        {
            var rootEntries = EntryStore.LoadCurrentDirCache();
            var re = rootEntries.FirstOrDefault();
            re?.ApplyMd5Checksum();
        }

        static void CreateCache2(string path)
        {
            //Process objProcess = Process.GetCurrentProcess();
            //long gcMemStart = GC.GetTotalMemory(true);
            //long processMemStart = objProcess.PrivateMemorySize64;

            var e = new EntryStore { SimpleScanCountEvent = ScanCountPrintDot, SimpleScanEndEvent = ScanEndofEntries, EntryCountThreshold = 10000 };
            e.SetRoot(path);
            e.Root.ScanStartUTC = DateTime.UtcNow;
            e.RecurseTree();
            e.Root.ScanEndUTC = DateTime.UtcNow;
            e.SaveToFile();
            var scanTimeSpan = (e.Root.ScanEndUTC - e.Root.ScanStartUTC);
            Console.WriteLine("Scanned Path {0}", e.Root.Path);
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
                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                    return;
                }

                var oldRoot = RootEntry.LoadDirCache(re.DefaultFileName);
                if (oldRoot != null)
                {
                    Console.WriteLine("Found cache \"{0}\"", re.DefaultFileName);
                    Console.WriteLine("Updating hashs on new scan from found cache file.");
                    oldRoot.TraverseTreesCopyHash(re);
                }
                re.SortAllChildrenByPath();
                re.SaveRootEntry();
                var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
                Console.WriteLine("Scanned Path {0}", re.Path);
                Console.WriteLine("Scan time {0:0.00} msecs", scanTimeSpan.TotalMilliseconds);
                Console.WriteLine("Saved Scanned Path {0}", re.DefaultFileName);
                Console.WriteLine("Files {0:0,0} Dirs {1:0,0} Total Size of Files {2:0,0}", re.FileEntryCount, re.DirEntryCount, re.Size);
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
            Console.WriteLine(string.Empty);
        }

        private static void PrintPathsHaveHashEnumerator()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var pdee = CommonEntry.GetPairDirEntries(rootEntries);
            foreach (var pairDirEntry in pdee)      
            {
                var hash = pairDirEntry.ChildDE.IsHashDone ? "#" : " ";
                var bang = pairDirEntry.PathProblem ? "!" : " ";
                Console.WriteLine("{0}{1}{2}", hash, bang, pairDirEntry.FullPath);
                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            }
        }
        
        private static void FindPouplous(int minimumCount)
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var entries = CommonEntry.GetDirEntries(rootEntries);
            var largeEntries = entries
                .Where(e => e.Children != null && e.Children.Count > minimumCount)
                .ToList();
            largeEntries.Sort(CompareDirEntries);

            foreach (var e in largeEntries.Where(e => e.Children != null && e.Children.Count > minimumCount))
            {
                Console.WriteLine("{0} {1}", e.FullPath, e.Children.Count);
                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            }
        }

        private static int CompareDirEntries(DirEntry x, DirEntry y)
        {
            return y.Children.Count - x.Children.Count;
        }
    }
}
