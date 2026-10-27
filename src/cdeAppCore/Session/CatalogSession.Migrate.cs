using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Catalog;
using Serilog.Core;

namespace cdeAppCore.Session;

// Legacy .cde catalogs: with LoadLegacyCatalogs off they are listed (not loaded) by every load, and
// MigrateLegacyCatalogsAsync converts the listed ones to .cdex. A migration is a catalog job like a rescan or a
// create: it is refused while any other job or a load runs, and they are refused while it runs.
public sealed partial class CatalogSession
{
    // The unmigrated .cde files the last load found. Replaced wholesale (never mutated). Guarded by _gate.
    private IReadOnlyList<LegacyCatalogDto> _legacyCatalogs = [];

    // A migration is running. Guarded by _gate.
    private bool _migrating;

    /// <summary>
    /// Load legacy <c>.cde</c> catalogs when a folder has no <c>.cdex</c> (cdeWin). When false (the cde-tauri
    /// sidecar), they are never loaded: the unmigrated ones are listed in <see cref="LegacyCatalogs"/> instead.
    /// </summary>
    public bool LoadLegacyCatalogs { get; init; } = true;

    public IReadOnlyList<LegacyCatalogDto> LegacyCatalogs
    {
        get { lock (_gate) return _legacyCatalogs; }
    }

    public async Task<MigrateResult> MigrateLegacyCatalogsAsync(IProgress<MigrateProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        List<LegacyCatalogDto> todo;
        lock (_gate)
        {
            if (_loading) throw new CatalogJobBusyException("Cannot migrate catalogs while catalogs are being loaded.");
            if (_migrating || _creating > 0 || AnyRescanning)
                throw new CatalogJobBusyException("Another catalog job is running; try again when it finishes.");
            todo = _legacyCatalogs.Where(l => l.Writable).ToList();
            _migrating = true;
        }

        try
        {
            // Loading and writing catalogs is synchronous file-system work; keep it off the caller's thread.
            return await Task.Run(() => MigrateCore(todo, progress, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            lock (_gate) _migrating = false;
        }
    }

    private MigrateResult MigrateCore(List<LegacyCatalogDto> todo, IProgress<MigrateProgress> progress,
        CancellationToken ct)
    {
        var result = new MigrateResult { Total = todo.Count };
        var converted = new HashSet<string>(PathComparer);
        using var repository = new CatalogRepository(_logger ?? Logger.None);
        var migrator = new CatalogMigrator(repository);
        try
        {
            for (var i = 0; i < todo.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var path = todo[i].Path;
                progress?.Report(new MigrateProgress(i + 1, todo.Count, path));
                var r = migrator.Migrate(path, ct);
                switch (r.Outcome)
                {
                    case MigrationOutcome.Migrated:
                        result.Migrated++;
                        converted.Add(path);
                        break;
                    case MigrationOutcome.MigratedNotRenamed:
                        result.Migrated++;
                        converted.Add(path);
                        result.Warnings.Add(new MigrateIssue { Path = path, Message = r.Message });
                        break;
                    case MigrationOutcome.Skipped:
                        // A .cdex appeared since the load listed it; the next load picks that up.
                        converted.Add(path);
                        result.Warnings.Add(new MigrateIssue { Path = path, Message = r.Message });
                        break;
                    default:
                        result.Failures.Add(new MigrateIssue { Path = path, Message = r.Message });
                        break;
                }
            }
        }
        finally
        {
            // Converted catalogs are no longer legacy, even when a cancel stops the run part-way.
            lock (_gate) _legacyCatalogs = _legacyCatalogs.Where(l => !converted.Contains(l.Path)).ToList();
        }

        return result;
    }

    // The unmigrated .cde files a load of configPath can see, with their size and whether their folder is writable.
    private List<LegacyCatalogDto> FindLegacyCatalogs(string configPath)
    {
        var writable = new Dictionary<string, bool>(PathComparer);
        var legacy = new List<LegacyCatalogDto>();
        foreach (var file in CatalogMigrator.Unmigrated(_loadCatalogService.GetLegacyFiles(configPath) ?? []))
        {
            var info = new FileInfo(file);
            if (!info.Exists) continue;
            var dir = info.DirectoryName ?? "";
            if (!writable.TryGetValue(dir, out var canWrite)) writable[dir] = canWrite = IsWritable(dir);
            legacy.Add(new LegacyCatalogDto { Path = info.FullName, SizeBytes = info.Length, Writable = canWrite });
        }

        return legacy;
    }
}
