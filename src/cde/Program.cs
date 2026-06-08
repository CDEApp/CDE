using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using cde.CommandLine;
using cdeLib;
using CommandLine;
using Serilog;
using SerilogTimings;
using FindOptions = cde.CommandLine.FindOptions;
using IContainer = Autofac.IContainer;

namespace cde;

public static class Program
{
    private static IContainer _container;
    private static CdeApp _app;

    /// <summary>
    /// Initialize the program. Returns false if initialization failed (e.g., missing config).
    /// </summary>
    public static bool InitProgram(string[] args)
    {
        if (!AppContainerBuilder.TryBuildContainer(args, out _container))
        {
            return false;
        }
        _app = _container.Resolve<CdeApp>();
        return true;
    }

    // Static entry points retained for cdeLibTest/DuplicationTest, which drives a scan+hash via Program.
    // These block on the async commands; the blocking is confined to this test-support path, not Main.
    public static void CreateCache(ScanOptions opts) => _app.CreateCacheAsync(opts).GetAwaiter().GetResult();
    public static void HashCatalog() => _app.HashCatalogAsync().GetAwaiter().GetResult();

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

    private static async Task<int> Main(string[] args)
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
                var parsed = GetParserResult(args);
                if (parsed is Parsed<object> ok)
                {
                    await DispatchAsync(ok.Value).ConfigureAwait(false);
                }
                else
                {
                    CustomHelpText.DisplayHelp(parsed);
                }
                return 0;
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Routes a parsed verb to its command. Bus-backed commands are awaited directly; the synchronous
    /// (interactive / inspection) commands are adapted to a completed task via <see cref="RunSync"/>.
    /// </summary>
    private static Task DispatchAsync(object options) => options switch
    {
        ScanOptions o            => _app.CreateCacheAsync(o),
        FindOptions o            => RunSync(() => _app.RunFind(o.Value, "--find")),
        FindPathOptions o        => RunSync(() => _app.RunFind(o.Value, "--findpath")),
        GrepOptions o            => RunSync(() => _app.RunFind(o.Value, "--grep")),
        GrepPathOptions o        => RunSync(() => _app.RunFind(o.Value, "--greppath")),
        ReplGrepPathOptions o    => RunSync(() => _app.FindRepl(FindService.ParamGrepPath, o.Value)),
        ReplGrepOptions o        => RunSync(() => _app.FindRepl(FindService.ParamGrep, o.Value)),
        ReplFindOptions o        => RunSync(() => _app.FindRepl(FindService.ParamFind, o.Value)),
        MigrateOptions o         => RunSync(() => _app.Migrate(o)),
        HashOptions _            => _app.HashCatalogAsync(),
        DupesOptions _           => _app.FindDupesAsync(),
        TreeDumpOptions _        => RunSync(_app.PrintPathsHaveHash),
        LoadWaitOptions _        => RunSync(_app.LoadWait),
        ReplOptions _            => RunSync(_app.InvokeRepl),
        PopulousFoldersOptions o => RunSync(() => _app.FindPopulous(o.Count)),
        UpdateOptions o          => _app.UpdateAsync(o),
        _                        => Task.CompletedTask,
    };

    private static Task RunSync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static void BreakConsole(object sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("\n * Break key detected. will exit as soon as current file process is completed.");
        Hack.BreakConsoleFlag = true;
        e.Cancel = true;
    }
}
