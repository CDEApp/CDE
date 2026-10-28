using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using cdeLib.Catalog;

namespace cdeLib.Hashing;

/// <summary>
/// The catalogs a <c>cde hash</c> run works on: one named <c>.cdex</c>, or every <c>.cdex</c> in the current directory
/// and one level down. Hashing compares file sizes only within the catalogs being hashed, so hashing one catalog
/// leaves a file whose only same-size match is in another catalog for a full run.
/// </summary>
public static class HashCatalogTargets
{
    /// <param name="catalogPath">The catalog to hash, or null / blank for every catalog found.</param>
    /// <param name="repository">Finds the catalogs when none is named.</param>
    /// <param name="problem">Why there is nothing to hash, when the result is empty; otherwise null.</param>
    public static IList<string> Resolve(string catalogPath, ICatalogRepository repository, out string problem)
    {
        ArgumentNullException.ThrowIfNull(repository);
        problem = null;

        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            var all = repository.GetColumnarFileList(["./"]);
            if (all.Count == 0) problem = "No .cdex catalogs found. Run 'cde migrate' to create them first.";
            return all;
        }

        var extension = Path.GetExtension(catalogPath);
        if (!File.Exists(catalogPath))
            problem = $"Catalog file not found: {catalogPath}";
        else if (string.Equals(extension, ".cde", StringComparison.OrdinalIgnoreCase))
            problem = $"{catalogPath} is a legacy .cde catalog. Run 'cde migrate {catalogPath}' to convert it first.";
        else if (!string.Equals(extension, ".cdex", StringComparison.OrdinalIgnoreCase))
            problem = $"{catalogPath} is not a .cdex catalog.";

        return problem == null ? [Path.GetFullPath(catalogPath)] : [];
    }

    /// <summary>
    /// <paramref name="cdexFiles"/> without the catalogs whose root can't be reached (see <see cref="CatalogRootProbe"/>),
    /// warning about each one left out: hashing them would only fail every file, slowly on an offline share.
    /// </summary>
    public static async Task<IList<string>> ReachableAsync(IList<string> cdexFiles, Serilog.ILogger logger,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var check = await CatalogRootProbe.CheckAsync(cdexFiles, timeout ?? CatalogRootProbe.DefaultTimeout)
            .ConfigureAwait(false);
        foreach (var skipped in check.Unreachable)
        {
            logger.Warning("Skipping {Catalog}: root {Root} {Reason}.", skipped.CatalogPath, skipped.RootPath,
                skipped.Reason);
        }

        if (check.Reachable.Count == 0 && check.Unreachable.Count > 0)
            logger.Warning("None of the catalogs can be reached; nothing to hash.");
        return check.Reachable;
    }
}
