using System;
using System.Collections.Generic;
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
    public static void CreateCache(ScanOptions opts) => _app.CreateCache(opts);
    public static void HashCatalog() => _app.HashCatalog();

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
                var parsedResult = GetParserResult(args)
                    .WithParsed<ScanOptions>(_app.CreateCache)
                    .WithParsed<FindOptions>(opts => _app.RunFind(opts.Value, "--find"))
                    .WithParsed<FindPathOptions>(opts => _app.RunFind(opts.Value, "--findpath"))
                    .WithParsed<GrepOptions>(opts => _app.RunFind(opts.Value, "--grep"))
                    .WithParsed<GrepPathOptions>(opts => _app.RunFind(opts.Value, "--greppath"))
                    .WithParsed<ReplGrepPathOptions>(opts => _app.FindRepl(FindService.ParamGrepPath, opts.Value))
                    .WithParsed<ReplGrepOptions>(opts => _app.FindRepl(FindService.ParamGrep, opts.Value))
                    .WithParsed<ReplFindOptions>(opts => _app.FindRepl(FindService.ParamFind, opts.Value))
                    .WithParsed<MigrateOptions>(_app.Migrate)
                    .WithParsed<HashOptions>(_ => _app.HashCatalog())
                    .WithParsed<DupesOptions>(_ => _app.FindDupes())
                    .WithParsed<TreeDumpOptions>(_ => _app.PrintPathsHaveHash())
                    .WithParsed<LoadWaitOptions>(_ => _app.LoadWait())
                    .WithParsed<ReplOptions>(_ => _app.InvokeRepl())
                    .WithParsed<PopulousFoldersOptions>(opts => _app.FindPopulous(opts.Count))
                    .WithParsed<UpdateOptions>(_app.Update);
                parsedResult.WithNotParsed(errs => CustomHelpText.DisplayHelp(parsedResult));
                return 0;
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void BreakConsole(object sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("\n * Break key detected. will exit as soon as current file process is completed.");
        Hack.BreakConsoleFlag = true;
        e.Cancel = true;
    }
}
