using System;
using System.IO;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeMemProbe;
using Serilog;

namespace cdeBenchmarks;

/// <summary>
/// Thin helper that turns the shared <see cref="SyntheticCatalog"/> generator into the two shapes
/// the benchmarks need: an in-memory tree (for search) and an on-disk <c>.cde</c> file (for load).
/// Centralised so every benchmark and phase measures the identical fixture.
/// </summary>
internal static class CatalogFixture
{
    private static readonly ILogger Silent = new LoggerConfiguration().CreateLogger();

    /// <summary>Build the synthetic tree in memory (no disk I/O).</summary>
    public static RootEntry BuildInMemory(int entryCount, bool withHashes)
        => SyntheticCatalog.Generate(entryCount, withHashes);

    /// <summary>Generate the synthetic tree and serialize it to a fresh temp <c>.cde</c> file; returns the path.</summary>
    public static string WriteTemp(int entryCount, bool withHashes)
    {
        var root = SyntheticCatalog.Generate(entryCount, withHashes);
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cde-bench-{entryCount}{(withHashes ? "-hashed" : "")}-{Guid.NewGuid():N}.cde");
        root.ActualFileName = path;
        using var repo = new CatalogRepository(Silent);
        repo.Save(root).GetAwaiter().GetResult();
        return path;
    }

    /// <summary>Load a catalog from disk through the real production load path.</summary>
    public static RootEntry Load(string path)
    {
        using var repo = new CatalogRepository(Silent);
        return repo.LoadDirCacheAsync(path).GetAwaiter().GetResult();
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (path != null && File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; ignore.
        }
    }
}
