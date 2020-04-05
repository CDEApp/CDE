using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autofac;
using cdeLib;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using Mono.Terminal;
using IContainer = Autofac.IContainer;

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
            Container = BootStrapper.Components(args);
            Console.CancelKeyPress += BreakConsole;
            if (args.Length == 0)
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
                HashCatalog();
            }
            else if (args.Length == 1 && param0 == "--dupes")
            {
                FindDupes();
            }
            else if (args.Length == 1 && param0 == "--treedump1")
            {
                PrintPathsHaveHashEnumerator();
            }
            // else if (args.Length == 1 && param0 == "--treedump2")
            // {
            //     EntryStore.PrintPathsHaveHash();
            // }
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
            else if (args.Length == 1 && param0 == "--repl")
            {
                var le = new LineEditor(null);
                string s;
                var running = true;

                while (running && (s = le.Edit("shell> ", string.Empty)) != null)
                {
                    Console.WriteLine($"----> [{s}]");
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
                if (int.TryParse(args[1], out var count))
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
        private static void FindRepl(string paramString, string firstPattern)
        {
            Find.GetDirCache();
            Find.StaticFind(firstPattern, paramString);
            do
            {
                if (Hack.BreakConsoleFlag)
                    Hack.BreakConsoleFlag = false; //reset otherwise we'll get some weird behaviour in loop.
                Console.Write("Enter string to search <nothing exits>: ");
                var pattern = Console.ReadLine();
                if (pattern == string.Empty)
                {
                    Console.WriteLine("Exiting...");
                    break;
                }

                Find.StaticFind(pattern, paramString);
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
            Console.WriteLine("       output folders containing more than <minimumcount> entires.");
        }

        private static void FindDupes()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var duplication = Container.Resolve<Duplication>();
            duplication.FindDuplicates(rootEntries);
        }

        public static void HashCatalog()
        {
            var logger = Container.Resolve<ILogger>();
            var diagnostics = Container.Resolve<IApplicationDiagnostics>();
            logger.LogInfo("Memory pre-catload: {0}", diagnostics.GetMemoryAllocated().FormatAsBytes());
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
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            Console.WriteLine($"Hash took : {elapsedTime}");
        }

        public static void CreateCache(string path)
        {
            var re = new RootEntry(Container.Resolve<IConfiguration>());
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
                    Console.WriteLine($"Found cache \"{re.DefaultFileName}\"");
                    Console.WriteLine("Updating hashes on new scan from found cache file.");
                    oldRoot.TraverseTreesCopyHash(re);
                }

                re.SortAllChildrenByPath();
                re.SaveRootEntry();
                var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
                Console.WriteLine($"Scanned Path {re.Path}");
                Console.WriteLine($"Scan time {scanTimeSpan.TotalMilliseconds:0.00} msecs");
                Console.WriteLine($"Saved Scanned Path {re.DefaultFileName}");
                Console.WriteLine(
                    $"Files {re.FileEntryCount:0,0} Dirs {re.DirEntryCount:0,0} Total Size of Files {re.Size:0,0}");
            }
            catch (ArgumentException aex)
            {
                Console.WriteLine($"Error: {aex.Message}");
            }
        }

        private static void PrintExceptions(string path, Exception ex)
        {
            Console.WriteLine($"Exception {ex.GetType()}, Path \"{path}\"");
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
            foreach (var pairDirEntry in CommonEntry.GetPairDirEntries(rootEntries))
            {
                var hash = pairDirEntry.ChildDE.IsHashDone ? "#" : " ";
                var bang = pairDirEntry.PathProblem ? "!" : " ";
                Console.WriteLine($"{hash}{bang}{pairDirEntry.FullPath}");
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
                Console.WriteLine($"{e.FullPath} {e.Children.Count}");
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