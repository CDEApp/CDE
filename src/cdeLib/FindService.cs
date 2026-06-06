using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cdeLib.Entities;
using Serilog;

namespace cdeLib;

public interface IFindService
{
    void Find(string pattern, string param, IList<RootEntry> rootEntries);
    void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);
    Task FindAsync(string pattern, string param, IList<RootEntry> rootEntries);
    Task FindAsync(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);

    bool IncludeFiles { get; set; }

    bool IncludeFolders { get; set; }
}

public class FindService : IFindService
{
    public const string ParamFind = "--find";
    public const string ParamFindPath = "--findpath";
    public const string ParamGrep = "--grep";
    public const string ParamGrepPath = "--greppath";

    public FindService()
    {
        IncludeFolders = true;
        IncludeFiles = true;
    }

    public bool IncludeFiles { get; set; }

    public bool IncludeFolders { get; set; }

    public void Find(string pattern, string param, IList<RootEntry> rootEntries)
    {
        var regexMode = param is ParamGrep or ParamGrepPath;
        var includePath = param is ParamGrepPath or ParamFindPath;
        Find(pattern, regexMode, includePath, rootEntries);
    }

    public void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
    {
        // Use the synchronous traversal: it is dramatically faster than the work-stealing async
        // path, which ran every entry through an async Task<bool> state machine plus per-entry
        // Task.Yield/Task.Delay. Measured on a 1M-entry catalog: ~49x faster for name search and
        // ~7x for path search (see src/cdeBenchmarks/baseline/search-baseline.md).
        var totalFound = 0L;
        var findOptions = new FindOptions
        {
            Pattern = pattern,
            RegexMode = regexMode,
            IncludePath = includePath,
            IncludeFiles = IncludeFiles,
            IncludeFolders = IncludeFolders,
            LimitResultCount = int.MaxValue,
            VisitorFunc = (p, d) =>
            {
                ++totalFound;
                Console.WriteLine(" {0}", p.MakeFullPath(d));
                return true;
            },
        };

        var timer = System.Diagnostics.Stopwatch.StartNew();
        findOptions.Find(rootEntries);
        timer.Stop();
        Log.Logger.Information(
            "Search Execution Time: {ExecutionTime}, Matching pattern {Pattern}, Total found {TotalFound}",
            timer.ElapsedMilliseconds, pattern, totalFound);
    }

    public Task FindAsync(string pattern, string param, IList<RootEntry> rootEntries)
    {
        var regexMode = param is ParamGrep or ParamGrepPath;
        var includePath = param is ParamGrepPath or ParamFindPath;
        return FindAsync(pattern, regexMode, includePath, rootEntries);
    }

    public Task FindAsync(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
    {
        // Search is CPU-bound; the synchronous path is the fast one. Keep the async signature for
        // API compatibility but run the fast core. Callers wanting off-thread execution should
        // wrap this in Task.Run themselves.
        Find(pattern, regexMode, includePath, rootEntries);
        return Task.CompletedTask;
    }
}