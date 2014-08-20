using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autofac;
using Mono.Terminal;
using cdeLib;
using cdeLib.Infrastructure;

namespace cde
{
    public static class Program
    {
        public const string Usage = @"cde - catalog directory entries.

Usage:
  cde --scan <path>...
  cde --hash [options]
  cde --hash2
  cde --dupes [options]
  cde --find <pattern> [options]
  cde --findPath <pattern> [options]
  cde --grep <pattern> [options]
  cde --grepPath <pattern> [options]
  cde --replFind <pattern> [options]
  cde --replGrep <pattern> [options]
  cde --replGrepPath <pattern> [options]
  cde --repl
  cde --treedump1
  cde --treedump2
  cde --loadwait
  cde --loadwait2
  cde (-h | --help)
  cde (-v | --version)

Options:
  --scan  <path>...
        Creates a cde catalog file for each given path.
        Copies hashes from old catalogs into new
        one as long as entries match size, date and path.
  --hashall Calculate hash (MD5) for all entires in catalogs.
  --hash    Calculate hash (MD5) for entries.
            Only create MD5 for required entries for dupes to work.
  --dupes   Find duplicate entries, requires hash.

        Find catalog entries.
        Prefix repl version means (read-eval-print) variation. 
        Enter blank pattern in interactive to exit.
        Performs first search with given pattern, then prompts.

  --find <pattern>        entry name substring matches pattern.
  --findPath <pattern>    entry path substring matches pattern.
  --grep <pattern>        entry name regex matches pattern.
  --grepPath <pattern>    entry path regex matches pattern.
  --replFind <pattern>       
  --replGrep <pattern>
  --replGrepPath <pattern>

        Include or exclude files, paths and catalogs.

  --includefiles <incfile>...     default all files
  --includeCatalogs <incCat>...   default all catalogs
  --excludeCatalogs <exclCat>...
  --excludepaths <exclPath>
  --excludefiles <exclfile>...

  --startpaths <startPath>...
        For --hash and --dupes, use specified paths only.
        Defaults to root of all catalogs.

        Extra matching rule for entry.
  --filter <filter>          regex pattern to entry name
  --filterpath <filterpath>  regex pattern to entry path

  --treedump1    debug output entry tree
  --treedump2    debug output entry tree
  --loadwait     debug load performance
  --loadwait2    debug load performance
  -h --help      Show this help screen.
  -v --version   Show version.

  (--filter | --filterPath)
  (--find | --grep | --findPath | --grepPath | --replfind | --replgrep | --replgreppath)
";
        string ExampleUsage = @"

  naval_fate.exe ship <name> move <x> <y> [--speed=<kn>]
  naval_fate.exe ship shoot <x> <y>
  naval_fate.exe mine (set|remove) <x> <y> [--moored | --drifting]
  naval_fate.exe (-h | --help)
  naval_fate.exe --version

Options:
  -h --help     Show this screen.
  --version     Show version.
  --speed=<kn>  Speed in knots [default: 10].
  --moored      Moored (anchored) mine.
  --drifting    Drifting mine.
";

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

            foreach (var rootEntry in rootEntries)
            {
                logger.LogDebug(String.Format("Saving {0}", rootEntry.DefaultFileName));
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
                Console.WriteLine("{0}{1}", hash, pairDirEntry.FullPath);                           
                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            }
        }
    }
}
