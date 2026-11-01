using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using cde.CommandLine;
using cdeLib;
using cdeLib.Hashing;
using cdeLib.Output;
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
    private static OperationCancellation _cancellation;

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
        _cancellation = _container.Resolve<OperationCancellation>();
        return true;
    }

    // Static entry points retained for cdeLibTest/DuplicationTest, which drives a scan+hash via Program.
    // These block on the async commands; the blocking is confined to this test-support path, not Main.
    public static void CreateCache(ScanOptions opts) => _app.CreateCacheAsync(opts).GetAwaiter().GetResult();
    public static void HashCatalog() => _app.HashCatalogAsync().GetAwaiter().GetResult();

    // The generic ParseArguments<T1..T16> overloads stop at 16 verbs, so the verbs are listed as an array instead.
    private static readonly Type[] VerbTypes =
    [
        typeof(ScanOptions),
        typeof(FindOptions),
        typeof(EverythingOptions),
        typeof(GrepOptions),
        typeof(GrepPathOptions),
        typeof(ReplGrepPathOptions),
        typeof(ReplGrepOptions),
        typeof(ReplFindOptions),
        typeof(MigrateOptions),
        typeof(HashOptions),
        typeof(DupesOptions),
        typeof(TreeDumpOptions),
        typeof(ReplOptions),
        typeof(PopulousFoldersOptions),
        typeof(FindPathOptions),
        typeof(UpdateOptions),
    ];

    private static ParserResult<object> GetParserResult(IEnumerable<string> args)
    {
        var parser = CommandLineParserBuilder.Build();
        return parser.ParseArguments(args, VerbTypes);
    }

    private static async Task<int> Main(string[] args)
    {
        // With --json or --csv, standard output is for the data alone: send the rest of the console output to standard error.
        if (MachineOutput.IsRequested(args)) MachineOutput.Redirect();
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
        catch (FormatException ex)
        {
            // a bad --template: report it plainly (no stack trace) and fail the command
            await Console.Error.WriteLineAsync($"error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (Exception ex) when (FindHashFailure(ex) is { } hashFailure)
        {
            // a hash run that did not complete (the hashes computed so far were saved): summary line, exit 1
            await Console.Error.WriteLineAsync(hashFailure.Message).ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            // Ctrl-C stops long operations cooperatively; one that ends with this exception has still stopped cleanly.
            Console.WriteLine(" * Cancelled.");
            return 130; // the conventional exit code for a command interrupted by Ctrl-C
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    // The message bus or Task plumbing may wrap the exception, so look through the inner exceptions.
    private static HashFailedException FindHashFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is HashFailedException h) return h;
            if (e is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (FindHashFailure(inner) is { } found) return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Routes a parsed verb to its command. Bus-backed commands are awaited directly; the synchronous
    /// (interactive / inspection) commands are adapted to a completed task via <see cref="RunSync"/>.
    /// </summary>
    private static Task DispatchAsync(object options) => options switch
    {
        ScanOptions o => _app.CreateCacheAsync(o),
        FindOptions o => RunFind(o, o.Value, "--find"),
        FindPathOptions o => RunFind(o, o.Value, "--findpath"),
        EverythingOptions o => RunFind(o, o.Value, FindService.ParamFind, everything: true),
        GrepOptions o => RunFind(o, o.Value, "--grep"),
        GrepPathOptions o => RunFind(o, o.Value, "--greppath"),
        ReplGrepPathOptions o => RunSync(() => _app.FindRepl(FindService.ParamGrepPath, o.Value)),
        ReplGrepOptions o => RunSync(() => _app.FindRepl(FindService.ParamGrep, o.Value)),
        ReplFindOptions o => RunSync(() => _app.FindRepl(FindService.ParamFind, o.Value)),
        MigrateOptions o => RunSync(() => _app.Migrate(o)),
        HashOptions o => HashAsync(o),
        DupesOptions o => DupesAsync(o),
        TreeDumpOptions => RunSync(_app.PrintPathsHaveHash),
        ReplOptions => RunSync(CdeApp.InvokeRepl),
        PopulousFoldersOptions o => RunSync(() => _app.FindPopulous(o.Count)),
        UpdateOptions o => _app.UpdateAsync(o),
        _ => Task.CompletedTask,
    };

    // The format is resolved first, so a bad combination of options fails before anything is written.
    private static Task RunFind(MatchOptions o, string value, string param, bool everything = false) => RunSync(() =>
    {
        using var output = MachineOutput.Open(o.ResolveFormat(), EntryRecords.FindColumns);
        _app.RunFind(value, param, o.Template, o.Filter, everything, output?.Writer);
    });

    private static async Task HashAsync(HashOptions o)
    {
        using var output = MachineOutput.Open(o.ResolveFormat(), EntryRecords.HashColumns);
        await _app.HashCatalogAsync(o.Path, output?.Writer).ConfigureAwait(false);
    }

    private static async Task DupesAsync(DupesOptions o)
    {
        using var output = MachineOutput.Open(o.ResolveFormat(), EntryRecords.DupesColumns);
        await _app.FindDupesAsync(output?.Writer).ConfigureAwait(false);
    }

    private static Task RunSync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static void BreakConsole(object sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("\n * Break key detected. Please wait... finishing up tasks.");
        _cancellation.Cancel();
        e.Cancel = true;
    }
}
