using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cde.CommandLine;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Hashing;
using cdeLib.Upgrade;
using Mono.Terminal;
using Serilog;
using SlimMessageBus;

namespace cde;

/// <summary>
/// Hosts the cde command implementations with their dependencies injected, keeping the logic free of
/// service-locator lookups and unit-testable in isolation. <see cref="Program"/> resolves a single
/// instance and dispatches parsed CLI verbs to it.
/// </summary>
public sealed class CdeApp(
    IFindService findService,
    ICatalogRepository repository,
    IMessageBus messageBus,
    OperationCancellation cancellation)
{
    // ---- catalog commands (routed through the message bus) ----

    public Task CreateCacheAsync(ScanOptions opts) =>
        messageBus.Send(new CreateCacheCommand(opts.Path)
            { Description = opts.Description, FollowJunctions = opts.FollowJunctions });

    public Task HashCatalogAsync() =>
        messageBus.Send(new HashCatalogCommand());

    public Task FindDupesAsync() =>
        messageBus.Send(new FindDuplicatesCommand());

    public Task UpdateAsync(UpdateOptions opts) =>
        messageBus.Send(new UpdateCommand { FileName = opts.FileName, Description = opts.Description });

    // ---- find ----

    /// <summary>
    /// Run a find, preferring the zero-copy columnar format: if any <c>.cdex</c> catalogs exist in the
    /// current dir (or one level down) they are searched over their memory maps with no managed catalog
    /// load; otherwise we fall back to loading the MessagePack <c>.cde</c> trees.
    /// </summary>
    public void RunFind(string value, string param)
    {
        var cdex = repository.GetColumnarFileList(["./"]);
        if (cdex.Count == 0)
        {
            findService.Find(value, param, repository.LoadCurrentDirCache());
            return;
        }

        var readers = new List<ColumnarCatalogReader>(cdex.Count);
        try
        {
            foreach (var file in cdex)
            {
                try
                {
                    readers.Add(new ColumnarCatalogReader(file));
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "Skipping unreadable .cdex {File}", file);
                }
            }

            findService.FindColumnar(value, param, readers);
        }
        finally
        {
            foreach (var reader in readers) reader.Dispose();
        }
    }

    // repl = read-eval-print-loop
    public void FindRepl(string paramString, string firstPattern)
    {
        var rootEntries = repository.LoadCurrentDirCache();

        if (!string.IsNullOrEmpty(firstPattern))
            findService.Find(firstPattern, paramString, rootEntries);

        Console.WriteLine("Issue --help for available params");

        while (true)
        {
            cancellation.Reset(); // fresh token each prompt so a break during the previous search doesn't carry over.
            Console.Write("Enter string to search <nothing exits>: ");
            var pattern = Console.ReadLine();
            if (string.IsNullOrEmpty(pattern))
            {
                Console.WriteLine("Exiting...");
                break;
            }

            if (pattern.StartsWith("--", StringComparison.CurrentCulture))
            {
                HandleReplCommand(pattern[2..]);
            }
            else
            {
                findService.Find(pattern, paramString, rootEntries);
            }
        }
    }

    private void HandleReplCommand(string command)
    {
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

    // ---- migrate ----

    /// <summary>
    /// One-way migration of MessagePack .cde catalogs to the zero-copy columnar .cdex format. With a
    /// path argument, converts that file; otherwise converts every catalog discovered in the current
    /// directory and one level down, writing a .cdex beside each source.
    /// </summary>
    public void Migrate(MigrateOptions opts)
    {
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
            files = repository.GetCacheFileList(["./"]).ToList();
        }

        if (files.Count == 0)
        {
            Console.WriteLine("No .cde catalogs found to migrate.");
            return;
        }

        var converted = 0;
        foreach (var file in files)
        {
            if (MigrateOne(file)) converted++;
        }

        Console.WriteLine($"Migrated {converted} of {files.Count} catalog(s) to .cdex.");
    }

    private bool MigrateOne(string file)
    {
        try
        {
            var root = repository.LoadDirCache(file);
            if (root == null)
            {
                Console.WriteLine($"  skip (could not load): {file}");
                return false;
            }

            var store = EntryStore.Build(root);
            var outFile = Path.ChangeExtension(file, ".cdex");
            ColumnarFormat.Write(store, outFile);

            var srcLen = new FileInfo(file).Length;
            var dstLen = new FileInfo(outFile).Length;
            Console.WriteLine(
                $"  {Path.GetFileName(file)} ({srcLen:N0} B) -> {Path.GetFileName(outFile)} " +
                $"({dstLen:N0} B, {store.Count:N0} entries)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  error migrating {file}: {ex.Message}");
            return false;
        }
    }

    // ---- repl / inspection ----

    public void InvokeRepl()
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
                    PrintReplHelp();
                    break;
            }
        }
    }

    private static void PrintReplHelp()
    {
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
    }

    public void LoadWait()
    {
        repository.LoadCurrentDirCache();
        Console.ReadLine();
    }

    public void PrintPathsHaveHash()
    {
        var rootEntries = repository.LoadCurrentDirCache();
        foreach (var pairDirEntry in EntryHelper.GetPairDirEntries(rootEntries))
        {
            var hash = pairDirEntry.ChildDE.IsHashDone ? "#" : " ";
            var bang = pairDirEntry.PathProblem ? "!" : " ";
            Console.WriteLine($"{hash}{bang}{pairDirEntry.FullPath}");
            if (cancellation.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public void FindPopulous(int minimumCount)
    {
        var largeEntries = EntryHelper.GetDirEntries(repository.LoadCurrentDirCache())
            .Where(e => e.Children is { } c && c.Count > minimumCount)
            .OrderByDescending(e => e.Children.Count)
            .ToList();

        foreach (var e in largeEntries)
        {
            Console.WriteLine($"{e.FullPath} {e.Children.Count}");
            if (cancellation.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
