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
        // Use async version for better performance
        FindAsync(pattern, regexMode, includePath, rootEntries).GetAwaiter().GetResult();
    }

    public async Task FindAsync(string pattern, string param, IList<RootEntry> rootEntries)
    {
        var regexMode = param is ParamGrep or ParamGrepPath;
        var includePath = param is ParamGrepPath or ParamFindPath;
        await FindAsync(pattern, regexMode, includePath, rootEntries);
    }

    public async Task FindAsync(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
    {
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
        await findOptions.FindAsync(rootEntries);
        timer.Stop();
        Log.Logger.Information(
            "Search Execution Time: {ExecutionTime}, Matching pattern {Pattern}, Total found {TotalFound}",
            timer.ElapsedMilliseconds, pattern, totalFound);
    }
}