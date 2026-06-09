using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using Serilog;

namespace cdeAppCore;

public interface ILoadCatalogService
{
    Task<List<RootEntry>> LoadRootEntriesAsync(
        string configPath,
        Action<int, int, string> progressCallback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover columnar <c>.cdex</c> catalogs (current dir + config path, one level down). When any
    /// exist the GUI memory-maps them instead of loading <c>.cde</c> trees. Empty/none ⇒ fall back.
    /// </summary>
    IList<string> GetColumnarFiles(string configPath);
}

/// <summary>
/// Frontend-agnostic catalog loading. Sources catalogs from the current directory and the supplied
/// <c>configPath</c> (one level down). No UI dependency: progress is reported via a plain callback,
/// cancellation via a <see cref="CancellationToken"/>.
/// </summary>
public class LoadCatalogService : ILoadCatalogService
{
    private readonly ILogger _logger;

    public LoadCatalogService(ILogger logger)
    {
        _logger = logger;
    }

    private static string[] CachePaths(string configPath) => [".", configPath];

    public IList<string> GetColumnarFiles(string configPath)
    {
        using var repo = new CatalogRepository(_logger);
        return repo.GetColumnarFileList(CachePaths(configPath));
    }

    public async Task<List<RootEntry>> LoadRootEntriesAsync(
        string configPath,
        Action<int, int, string> progressCallback,
        CancellationToken cancellationToken = default)
    {
        using var repo = new CatalogRepository(_logger);
        var cacheFiles = repo.GetCacheFileList(CachePaths(configPath));
        var totalFiles = cacheFiles.Count;

        if (totalFiles == 0)
        {
            progressCallback?.Invoke(0, 0, "No catalogs found");
            return [];
        }

        var volatileFileCounter = 0;
        var progressReportThreshold = Math.Max(1, totalFiles / 50);
        var lastProgressReport = DateTime.UtcNow;
        var progressReportInterval = TimeSpan.FromMilliseconds(100);

        var rootEntries = new ConcurrentBag<RootEntry>();

        progressCallback?.Invoke(0, totalFiles, $"Loading catalog 0 of {totalFiles}...");

        var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount * 2, 8));
        var tasks = cacheFiles.Select(async cacheFile =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var re = await LoadCatalogOptimizedAsync(repo, cacheFile, cancellationToken);
                if (re != null)
                {
                    var currentCount = Interlocked.Increment(ref volatileFileCounter);
                    rootEntries.Add(re);

                    var now = DateTime.UtcNow;
                    if (currentCount % progressReportThreshold == 0 ||
                        now - lastProgressReport > progressReportInterval)
                    {
                        progressCallback?.Invoke(currentCount, totalFiles,
                            $"Loading catalog {currentCount} of {totalFiles}...");
                        lastProgressReport = now;
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var finalCount = volatileFileCounter;
        progressCallback?.Invoke(finalCount, totalFiles, $"Loaded {finalCount} catalogs");

        return rootEntries.ToList();
    }

    private async Task<RootEntry> LoadCatalogOptimizedAsync(
        CatalogRepository repo,
        string cacheFile,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(cacheFile)) return null;

            var fileInfo = new FileInfo(cacheFile);
            if (fileInfo.Length == 0) return null;

            cancellationToken.ThrowIfCancellationRequested();
            var rootEntry = await repo.LoadDirCacheAsync(cacheFile);
            return rootEntry;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load catalog file {CacheFile}, skipping", cacheFile);
            return null;
        }
    }
}
