using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;

namespace cdeLib.Catalog;

public enum MigrationOutcome
{
    /// <summary>The .cdex was written and the source renamed to <c>.cde.migrated</c>.</summary>
    Migrated,

    /// <summary>The .cdex was written but the source could not be renamed (it stays as <c>.cde</c>).</summary>
    MigratedNotRenamed,

    /// <summary>Nothing done: a .cdex of the same name already exists.</summary>
    Skipped,

    /// <summary>Nothing written: the .cde could not be loaded, or the .cdex could not be written.</summary>
    Failed
}

/// <summary>What happened to one catalog. <see cref="Message"/> explains anything other than <see cref="MigrationOutcome.Migrated"/>.</summary>
public sealed record MigrationResult(string SourcePath, string TargetPath, MigrationOutcome Outcome, int Entries, string Message);

/// <summary>
/// One-way conversion of a legacy MessagePack <c>.cde</c> catalog to the columnar <c>.cdex</c> format, used by
/// <c>cde migrate</c> and the GUIs. The <c>.cdex</c> is written beside the source through a <c>.tmp</c> and renamed
/// into place without overwriting, so a failure or cancellation leaves no partial catalog. The source is then renamed
/// to <c>.cde.migrated</c>, so catalog discovery no longer finds it; an existing <c>.cde.migrated</c> is never replaced.
/// </summary>
public sealed class CatalogMigrator(ICatalogRepository repository)
{
    public const string MigratedSuffix = ".migrated";

    public static string TargetPathFor(string cdeFile) => Path.ChangeExtension(cdeFile, ".cdex");

    public static string MigratedPathFor(string cdeFile) => cdeFile + MigratedSuffix;

    /// <summary>The .cde files with no .cdex of the same base name beside them, in their given order.</summary>
    public static List<string> Unmigrated(IEnumerable<string> cdeFiles) =>
        cdeFiles.Where(f => !File.Exists(TargetPathFor(f))).ToList();

    /// <summary>
    /// Converts <paramref name="cdeFile"/>. Never throws for a bad catalog or an I/O failure (see the result);
    /// throws <see cref="OperationCanceledException"/> when cancelled, with nothing written.
    /// </summary>
    public MigrationResult Migrate(string cdeFile, CancellationToken cancellationToken = default)
    {
        var target = TargetPathFor(cdeFile);
        if (File.Exists(target))
            return new MigrationResult(cdeFile, target, MigrationOutcome.Skipped, 0, $"{Path.GetFileName(target)} already exists");

        var tmp = target + ".tmp";
        int entries;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = repository.LoadDirCache(cdeFile);
            if (root == null)
                return new MigrationResult(cdeFile, target, MigrationOutcome.Failed, 0, "the catalog could not be loaded");

            var store = EntryStore.Build(root);
            entries = store.Count;
            cancellationToken.ThrowIfCancellationRequested();

            ColumnarFormat.Write(store, tmp);
            cancellationToken.ThrowIfCancellationRequested();

            // No overwrite, checked by the file system at rename time (covers a .cdex appearing meanwhile).
            File.Move(tmp, target, overwrite: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MigrationResult(cdeFile, target, MigrationOutcome.Failed, 0, ex.Message);
        }
        finally
        {
            // Already renamed into place on success; on any other exit, drop the partial file.
            TryDelete(tmp);
        }

        try
        {
            File.Move(cdeFile, MigratedPathFor(cdeFile), overwrite: false);
            return new MigrationResult(cdeFile, target, MigrationOutcome.Migrated, entries, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MigrationResult(cdeFile, target, MigrationOutcome.MigratedNotRenamed, entries,
                $"could not rename to {Path.GetFileName(MigratedPathFor(cdeFile))}: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort: a leftover .tmp is never loaded (discovery matches *.cdex only)
        }
    }
}
