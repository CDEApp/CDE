using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using cde.CommandLine;
using cdeLib;
using cdeLib.Cache;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Hashing;
using CommandLine;
using MediatR;
using Mono.Terminal;
using SerilogTimings;
using FindOptions = cde.CommandLine.FindOptions;
using IContainer = Autofac.IContainer;

namespace cde
{
    public static class Program
    {
        private static IContainer _container;
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
            _container = AppContainerBuilder.BuildContainer(args);
            Mediatr = _container.Resolve<IMediator>();
        }

        private static int Main(string[] args)
        {
            
                InitProgram(args);
                Console.CancelKeyPress += BreakConsole;
                using (Operation.Time("Main App"))
                {
                var findService = _container.Resolve<IFindService>();

                var parser = CommandLineParserBuilder.Build();
                parser.ParseArguments<ScanOptions, FindOptions, GrepOptions, GrepPathOptions, ReplGrepPathOptions,
                        ReplGrepOptions, ReplFindOptions,
                        HashOptions, DupesOptions, TreeDumpOptions, LoadWaitOptions, ReplOptions, PopulousFoldersOptions
                        ,
                        FindPathOptions>(
                        args)
                    .WithParsed<ScanOptions>(opts => CreateCache(opts.Path))
                    .WithParsed<FindOptions>(opts =>
                    {
                        findService.StaticFind(opts.Value, "--find",
                            _container.Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<FindPathOptions>(opts =>
                    {
                        findService.StaticFind(opts.Value, "--findpath",
                            _container.Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<GrepOptions>(opts =>
                    {
                        findService.StaticFind(opts.Value, "--grep",
                            _container.Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<GrepPathOptions>(opts =>
                    {
                        findService.StaticFind(opts.Value, "--greppath",
                            _container.Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<ReplGrepPathOptions>(opts => { FindRepl(FindService.ParamGreppath, opts.Value); })
                    .WithParsed<ReplGrepOptions>(opts => { FindRepl(FindService.ParamGrep, opts.Value); })
                    .WithParsed<ReplFindOptions>(opts => { FindRepl(FindService.ParamFind, opts.Value); })
                    .WithParsed<HashOptions>(opts => { HashCatalog(); })
                    .WithParsed<DupesOptions>(opts => { FindDupes(); })
                    .WithParsed<TreeDumpOptions>(opts => { PrintPathsHaveHashEnumerator(); })
                    .WithParsed<LoadWaitOptions>(opts =>
                    {
                        _container.Resolve<ICatalogRepository>().LoadCurrentDirCache();
                        Console.ReadLine();
                    })
                    .WithParsed<ReplOptions>(opts => { InvokeRepl(); })
                    .WithParsed<PopulousFoldersOptions>(opts => { FindPopulous(opts.Count); })
                    .WithNotParsed(errs => { Environment.Exit(1); });
                return 0;
            }
        }

        private static void InvokeRepl()
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

        private static void BreakConsole(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("\n * Break key detected. will exit as soon as current file process is completed.");
            Hack.BreakConsoleFlag = true;
            e.Cancel = true;
        }

        // repl = read-eval-print-loop
        private static void FindRepl(string paramString, string firstPattern)
        {
            var rootEntries = _container.Resolve<ICatalogRepository>().LoadCurrentDirCache();
            var findService = _container.Resolve<IFindService>();

            findService.StaticFind(firstPattern, paramString, rootEntries);
            do
            {
                if (Hack.BreakConsoleFlag)
                    Hack.BreakConsoleFlag = false; //reset otherwise we'll get some weird behaviour in loop.
                Console.Write("Enter string to search <nothing exits>: ");
                var pattern = Console.ReadLine();
                if (string.IsNullOrEmpty(pattern))
                {
                    Console.WriteLine("Exiting...");
                    break;
                }

                findService.StaticFind(pattern, paramString, rootEntries);
            } while (true);
        }

        private static void FindDupes()
        {
            var task = Task.Run(async () => await Mediatr.Send(new FindDuplicatesCommand()).ConfigureAwait(false));
            task.Wait();
        }

        public static void HashCatalog()
        {
            var task = Task.Run(async () => await Mediatr.Send(new HashCatalogCommand()).ConfigureAwait(false));
            task.Wait();
        }

        public static int CreateCache(string path)
        {
            var task = Task.Run(async () => await Mediatr.Send(new CreateCacheCommand(path)).ConfigureAwait(false));
            task.Wait();
            return 0;
        }

        private static void PrintPathsHaveHashEnumerator()
        {
            var rootEntries = _container.Resolve<ICatalogRepository>().LoadCurrentDirCache();
            foreach (var pairDirEntry in EntryHelper.GetPairDirEntries(rootEntries))
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
            var rootEntries = _container.Resolve<ICatalogRepository>().LoadCurrentDirCache();
            var entries = EntryHelper.GetDirEntries(rootEntries);
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

        private static int CompareDirEntries(ICommonEntry x, ICommonEntry y)
        {
            return y.Children.Count - x.Children.Count;
        }
    }
}