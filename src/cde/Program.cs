using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using cde.CommandLine;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Hashing;
using cdeLib.Upgrade;
using CommandLine;
using SlimMessageBus;
using Mono.Terminal;
using Serilog;
using SerilogTimings;
using FindOptions = cde.CommandLine.FindOptions;
using IContainer = Autofac.IContainer;

namespace cde;

public static class Program
{
    private static IContainer _container;

    private static IMessageBus MessageBus { get; set; }

    /// <summary>
    /// Initialize the program. Returns false if initialization failed (e.g., missing config).
    /// </summary>
    public static bool InitProgram(string[] args)
    {
        _container = AppContainerBuilder.BuildContainer(args);
        if (_container == null)
        {
            return false;
        }
        MessageBus = Resolve<IMessageBus>();
        return true;
    }

    private static ParserResult<object> GetParserResult(IEnumerable<string> args)
    {
        var parser = CommandLineParserBuilder.Build();
        return parser.ParseArguments<
            ScanOptions,
            FindOptions,
            GrepOptions,
            GrepPathOptions,
            ReplGrepPathOptions,
            ReplGrepOptions,
            ReplFindOptions,
            MigrateOptions,
            HashOptions,
            DupesOptions,
            TreeDumpOptions,
            LoadWaitOptions,
            ReplOptions,
            PopulousFoldersOptions,
            FindPathOptions,
            UpdateOptions>(args);
    }

    private static int Main(string[] args)
    {
        if (!InitProgram(args))
        {
            return 1; // Exit with error code if initialization failed
        }
        Console.CancelKeyPress += BreakConsole;
        try
        {
            using (Operation.Time("App"))
            {
                var findService = Resolve<IFindService>();
                var parsedResult = GetParserResult(args)
                    .WithParsed<ScanOptions>(CreateCache)
                    .WithParsed<FindOptions>(opts =>
                    {
                        findService.Find(opts.Value, "--find",
                            Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<FindPathOptions>(opts =>
                    {
                        findService.Find(opts.Value, "--findpath",
                            Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<GrepOptions>(opts =>
                    {
                        findService.Find(opts.Value, "--grep",
                            Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<GrepPathOptions>(opts =>
                    {
                        findService.Find(opts.Value, "--greppath",
                            Resolve<ICatalogRepository>().LoadCurrentDirCache());
                    })
                    .WithParsed<ReplGrepPathOptions>(opts => FindRepl(FindService.ParamGrepPath, opts.Value))
                    .WithParsed<ReplGrepOptions>(opts => FindRepl(FindService.ParamGrep, opts.Value))
                    .WithParsed<ReplFindOptions>(opts => FindRepl(FindService.ParamFind, opts.Value))
                    .WithParsed<MigrateOptions>(Migrate)
                    .WithParsed<HashOptions>(_ => HashCatalog())
                    .WithParsed<DupesOptions>(_ => FindDupes())
                    .WithParsed<TreeDumpOptions>(_ => PrintPathsHaveHashEnumerator())
                    .WithParsed<LoadWaitOptions>(_ =>
                    {
                        Resolve<ICatalogRepository>().LoadCurrentDirCache();
                        Console.ReadLine();
                    })
                    .WithParsed<ReplOptions>(_ => InvokeRepl())
                    .WithParsed<PopulousFoldersOptions>(opts => FindPopulous(opts.Count))
                    .WithParsed<UpdateOptions>(Update);
                parsedResult.WithNotParsed(errs => CustomHelpText.DisplayHelp(parsedResult));
                return 0;
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static T Resolve<T>()
    {
        return _container.Resolve<T>();
    }

    /// <summary>
    /// One-way migration of MessagePack .cde catalogs to the zero-copy columnar .cdex format. With a
    /// path argument, converts that file; otherwise converts every catalog discovered in the current
    /// directory and one level down, writing a .cdex beside each source.
    /// </summary>
    private static void Migrate(MigrateOptions opts)
    {
        var repo = Resolve<ICatalogRepository>();

        List<string> files;
        if (!string.IsNullOrWhiteSpace(opts.Path))
        {
            if (!File.Exists(opts.Path))
            {
                Console.WriteLine($"File not found: {opts.Path}");
                return;
            }
            files = [opts.Path];
        }
        else
        {
            files = repo.GetCacheFileList(["./"]).ToList();
        }

        if (files.Count == 0)
        {
            Console.WriteLine("No .cde catalogs found to migrate.");
            return;
        }

        var converted = 0;
        foreach (var file in files)
        {
            try
            {
                var root = repo.LoadDirCache(file);
                if (root == null)
                {
                    Console.WriteLine($"  skip (could not load): {file}");
                    continue;
                }

                var store = EntryStore.Build(root);
                var outFile = Path.ChangeExtension(file, ".cdex");
                ColumnarFormat.Write(store, outFile);

                var srcLen = new FileInfo(file).Length;
                var dstLen = new FileInfo(outFile).Length;
                Console.WriteLine(
                    $"  {Path.GetFileName(file)} ({srcLen:N0} B) -> {Path.GetFileName(outFile)} " +
                    $"({dstLen:N0} B, {store.Count:N0} entries)");
                converted++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  error migrating {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"Migrated {converted} of {files.Count} catalog(s) to .cdex.");
    }

    private static void InvokeRepl()
    {
        var le = new LineEditor(name: null);
        var running = true;

        while (running && le.Edit("shell> ", string.Empty) is { } s)
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
                    Console.WriteLine("  quit - quit,");
                    Console.WriteLine("  help - show help, ? - show help");
                    Console.WriteLine("  history - show history, ! - show history");
                    Console.WriteLine("Keystrokes:");
                    Console.WriteLine("  Home, End, Left, Right,  Up, Down, Back, Del, Tab");
                    Console.WriteLine("  C-a,  C-e,  C-b,   C-f, C-p,  C-n,       C-d");
                    Console.WriteLine("  C-l - clear console to top");
                    Console.WriteLine("  C-r - reverse search history");
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
        var rootEntries = Resolve<ICatalogRepository>().LoadCurrentDirCache();
        var findService = Resolve<IFindService>();

        if (!string.IsNullOrEmpty(firstPattern))
            findService.Find(firstPattern, paramString, rootEntries);

        Console.WriteLine("Issue --help for available params");

        while (true)
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

            if (pattern.StartsWith("--", StringComparison.CurrentCulture))
            {
                var command = pattern[2..];
                switch (command.ToLower(CultureInfo.CurrentCulture))
                {
                    case "includefiles":
                        findService.IncludeFiles = !findService.IncludeFiles;
                        Console.WriteLine($"IncludeFiles:{findService.IncludeFiles}");
                        break;
                    case "includefolders":
                        findService.IncludeFolders = !findService.IncludeFolders;
                        Console.WriteLine($"IncludeFolders:{findService.IncludeFolders}");
                        break;
                    case "help":
                        Console.WriteLine("Valid options are");
                        Console.WriteLine("--includefiles");
                        Console.WriteLine("--includefolders");
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    default:
                        Console.WriteLine($"unknown command {command}");
                        break;
                }
            }
            else
            {
                findService.Find(pattern, paramString, rootEntries);
            }
        }
    }

    private static void Update(UpdateOptions opts)
    {
        var task = Task.Run(() =>
            MessageBus.Send(new UpdateCommand { FileName = opts.FileName, Description = opts.Description }));
        task.Wait();
    }

    private static void FindDupes()
    {
        var task = Task.Run(() => MessageBus.Send(new FindDuplicatesCommand()));
        task.Wait();
    }

    public static void HashCatalog()
    {
        var task = Task.Run(async () => await MessageBus.Send(new HashCatalogCommand()).ConfigureAwait(false));
        task.Wait();
    }

    public static void CreateCache(ScanOptions opts)
    {
        var task = Task.Run(async () =>
            await MessageBus.Send(new CreateCacheCommand(opts.Path) { Description = opts.Description })
                .ConfigureAwait(false));
        task.Wait();
    }

    private static void PrintPathsHaveHashEnumerator()
    {
        var rootEntries = Resolve<ICatalogRepository>().LoadCurrentDirCache();
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
        var rootEntries = Resolve<ICatalogRepository>().LoadCurrentDirCache();
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