using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using Serilog;

namespace cdeLib;

public interface IFindService
{
    void Find(string pattern, string param, IList<RootEntry> rootEntries);
    void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);

    /// <summary>
    /// Search columnar <c>.cdex</c> catalogs zero-copy over their memory maps (no managed catalog
    /// load). Mirrors <see cref="Find(string,string,IList{RootEntry})"/> result semantics.
    /// </summary>
    void FindColumnar(string pattern, string param, IList<ColumnarCatalogReader> readers);
    Task FindAsync(string pattern, string param, IList<RootEntry> rootEntries);
    Task FindAsync(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);

    bool IncludeFiles { get; set; }

    bool IncludeFolders { get; set; }

    /// <summary>How each match line is written; null writes the default, a leading space then the full path.</summary>
    MatchTemplate Template { get; set; }
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

    public MatchTemplate Template { get; set; }

    public void Find(string pattern, string param, IList<RootEntry> rootEntries)
    {
        var regexMode = param is ParamGrep or ParamGrepPath;
        var includePath = param is ParamGrepPath or ParamFindPath;
        Find(pattern, regexMode, includePath, rootEntries);
    }

    public void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
    {
        // Convert each loaded catalog to the struct-of-arrays EntryStore and release its pointer
        // tree before searching. The store holds the same catalog in ~1/3 the structural memory
        // (42 vs 129 bytes/entry; see src/cdeBenchmarks/baseline/soa-prototype.md), and the
        // index-based scan is cache friendly. The CLI find applies only pattern + name/path +
        // file/folder filtering, all of which EntryStoreSearch supports.
        var stores = new List<EntryStore>(rootEntries.Count);
        for (var i = 0; i < rootEntries.Count; i++)
        {
            if (rootEntries[i] != null) stores.Add(EntryStore.Build(rootEntries[i]));
            rootEntries[i] = null; // drop the tree so it can be collected while we search the stores
        }

        var totalFound = 0L;
        var timer = Stopwatch.StartNew();
        foreach (var store in stores)
        {
            EntryStoreSearch.Find(store, pattern, regexMode, includePath, IncludeFiles, IncludeFolders,
                idx =>
                {
                    ++totalFound;
                    WriteMatch(store, idx);
                });
        }

        timer.Stop();
        Log.Logger.Information(
            "Search Execution Time: {ExecutionTime}, Matching pattern {Pattern}, Total found {TotalFound}",
            timer.ElapsedMilliseconds, pattern, totalFound);
    }

    public void FindColumnar(string pattern, string param, IList<ColumnarCatalogReader> readers)
    {
        var regexMode = param is ParamGrep or ParamGrepPath;
        var includePath = param is ParamGrepPath or ParamFindPath;

        var totalFound = 0L;
        var timer = Stopwatch.StartNew();
        foreach (var reader in readers)
        {
            reader?.Find(pattern, regexMode, includePath, IncludeFiles, IncludeFolders,
                idx =>
                {
                    ++totalFound;
                    WriteMatch(reader, idx);
                });
        }

        timer.Stop();
        Log.Logger.Information(
            "Search Execution Time: {ExecutionTime}, Matching pattern {Pattern}, Total found {TotalFound}",
            timer.ElapsedMilliseconds, pattern, totalFound);
    }

    // Size and modified are read only when a template is set, so the default output pays nothing for them.
    private void WriteMatch(EntryStore store, int idx)
    {
        if (Template == null)
            Console.WriteLine(" {0}", store.FullPath(idx));
        else
            Console.WriteLine(Template.Render(store.ActualFileName ?? store.DefaultFileName, store.FullPath(idx),
                store.SizeOf(idx), store.ModifiedOf(idx)));
    }

    private void WriteMatch(ColumnarCatalogReader reader, int idx)
    {
        if (Template == null)
            Console.WriteLine(" {0}", reader.FullPath(idx));
        else
            Console.WriteLine(Template.Render(reader.FilePath, reader.FullPath(idx),
                reader.SizeOf(idx), reader.ModifiedOf(idx)));
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