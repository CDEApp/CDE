using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib.Entities.Columnar;

namespace cdeLib.Hashing;

/// <summary>A catalog left out of hashing because its root could not be reached.</summary>
public sealed record UnreachableCatalog(string CatalogPath, string RootPath, string Reason);

/// <summary>The catalogs to hash, in their given order, and the ones skipped.</summary>
public sealed record CatalogRootCheck(IList<string> Reachable, IReadOnlyList<UnreachableCatalog> Unreachable);

/// <summary>
/// Checks, before hashing, that each catalog's root (drive, share or folder) can be reached, so a catalog whose drive
/// is disconnected or whose share is offline is skipped instead of failing every file in it. Opening a file on an
/// unreachable share can block for tens of seconds, so each distinct root is checked once, all in parallel, and a
/// check that has not answered within the time limit counts as unreachable (it is left to finish in the background).
/// </summary>
public static class CatalogRootProbe
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <param name="cdexFiles">The catalogs to check.</param>
    /// <param name="timeout">How long a root may take to answer.</param>
    /// <param name="rootExists">The check for one root; defaults to <see cref="Directory.Exists"/>.</param>
    public static async Task<CatalogRootCheck> CheckAsync(IList<string> cdexFiles, TimeSpan timeout,
        Func<string, bool> rootExists = null)
    {
        ArgumentNullException.ThrowIfNull(cdexFiles);
        rootExists ??= Directory.Exists;

        // Null root: the catalog can't be read here; pass it on and let hashing report it as before.
        var roots = cdexFiles.Select(f => (File: f, Root: TryReadRoot(f))).ToList();
        var probes = roots
            .Where(r => r.Root != null)
            .Select(r => r.Root)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(root => root, root => Answer(root, timeout, rootExists), StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(probes.Values).ConfigureAwait(false);

        var reachable = new List<string>();
        var unreachable = new List<UnreachableCatalog>();
        foreach (var (file, root) in roots)
        {
            var reason = root == null ? null : probes[root].Result;
            if (reason == null) reachable.Add(file);
            else unreachable.Add(new UnreachableCatalog(file, root, reason));
        }

        return new CatalogRootCheck(reachable, unreachable);
    }

    // Null when the root answered that it exists, else why it is unreachable.
    private static async Task<string> Answer(string root, TimeSpan timeout, Func<string, bool> rootExists)
    {
        try
        {
            var exists = await Task.Run(() => rootExists(root)).WaitAsync(timeout).ConfigureAwait(false);
            return exists ? null : "not found (drive disconnected, share offline or folder moved)";
        }
        catch (TimeoutException)
        {
            return $"gave no answer within {timeout.TotalSeconds:0.#} s (share offline or host unreachable)";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    private static string TryReadRoot(string cdexFile)
    {
        try
        {
            using var reader = new ColumnarCatalogReader(cdexFile);
            return string.IsNullOrEmpty(reader.RootPath) ? null : reader.RootPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or ArgumentException)
        {
            return null;
        }
    }
}
