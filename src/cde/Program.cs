using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using cdeLib;
using cdeLib.Cache;
using cdeLib.Duplicates;
using cdeLib.Hashing;
using MediatR;
using Mono.Terminal;
using IContainer = Autofac.IContainer;

namespace cde
{
    public static class Program
    {
        public static IContainer Container;
        private static IMediator Mediatr { get; set; }


        public static string Version
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                return $"{fvi.ProductName} v{fvi.ProductMajorPart}.{fvi.ProductMinorPart}";
            }
        }

        public static void InitProgram(string[] args)
        {
            Container = AppContainerBuilder.BuildContainer(args);
            Mediatr = Container.Resolve<IMediator>();
        }

        private static void Main(string[] args)
        {
            InitProgram(args);
            Console.CancelKeyPress += BreakConsole;
            if (args.Length == 0)
            {
                Help.ShowHelp();
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
                    FindPopulous(count);
                }
                else
                {
                    Console.WriteLine("Populous folders option requires an integer as second parameter");
                }
            }
            else
            {
                Help.ShowHelp();
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

        private static void FindDupes()
        {
            var task = Task.Run(async () => await Mediatr.Send(new FindDuplicatesCommand()));
            task.Wait();
        }

        public static void HashCatalog()
        {
            var task = Task.Run(async () => await Mediatr.Send(new HashCatalogCommand()));
            task.Wait();
        }

        public static void CreateCache(string path)
        {
            var task = Task.Run(async () => await Mediatr.Send(new CreateCacheCommand(path)));
            task.Wait();
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

        private static void FindPopulous(int minimumCount)
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