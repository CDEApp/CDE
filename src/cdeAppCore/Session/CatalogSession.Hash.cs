using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;

namespace cdeAppCore.Session;

// The run reads through its own mappings of the files, not the session's, because it crosses threads and a read
// lease belongs to one thread.
public sealed partial class CatalogSession
{
    private const int HashProgressIntervalMs = 250;

    public JobError PreflightHash(IReadOnlyList<int> catalogIds, HashOptions options)
    {
        var refusal = ParseHashRequest(catalogIds, options, out var ids, out _);
        if (refusal != null) return refusal;

        List<CatalogSlot> slots;
        lock (_gate)
        {
            if (_loading || _migrating) return HashBusy();
            slots = _slots;
        }

        foreach (var id in ids)
        {
            if (id < 0 || id >= slots.Count) return UnknownHashCatalog(id);
            if (slots[id].IsRescanning) return HashAlreadyRunning(id);
            refusal = CheckHashable(id, slots[id]);
            if (refusal != null) return refusal;
        }

        return null;
    }

    public HashPreflightResult PreviewHash(IReadOnlyList<int> catalogIds, HashOptions options,
        CancellationToken cancellationToken = default)
    {
        var refusal = ParseHashRequest(catalogIds, options, out var ids, out var selection);
        if (refusal != null) throw new HashRefusedException(refusal);

        List<CatalogSlot> slots;
        lock (_gate)
        {
            if (_loading || _migrating) throw new HashRefusedException(HashBusy());
            slots = _slots;
        }

        var leases = new List<CatalogReadLease>(ids.Length);
        try
        {
            var readers = new List<ColumnarCatalogReader>(ids.Length);
            foreach (var id in ids)
            {
                if (id < 0 || id >= slots.Count) throw new HashRefusedException(UnknownHashCatalog(id));
                if (!slots[id].TryEnterRead(out var lease)) throw new HashRefusedException(HashUnavailable(id));
                leases.Add(lease);
                if (lease.Source is not ColumnarCatalogReader reader) throw new HashRefusedException(LegacyHashCatalog(id));
                readers.Add(reader);
            }

            var preview = ColumnarCatalogHashing.Preview(readers, selection, cancellationToken);
            return new HashPreflightResult
            {
                Files = preview.Files, Bytes = preview.Bytes, AlreadyHashed = preview.AlreadyHashed
            };
        }
        finally
        {
            foreach (var lease in leases) lease.Dispose();
        }
    }

    public async Task<HashResult> HashAsync(IReadOnlyList<int> catalogIds, HashOptions options,
        IProgress<HashProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var refusal = ParseHashRequest(catalogIds, options, out var ids, out var selection);
        if (refusal != null) throw new HashRefusedException(refusal);

        var slots = new CatalogSlot[ids.Length];
        lock (_gate)
        {
            if (_loading || _migrating) throw new HashRefusedException(HashBusy());
            foreach (var id in ids)
            {
                if (id < 0 || id >= _slots.Count) throw new HashRefusedException(UnknownHashCatalog(id));
            }

            for (var i = 0; i < ids.Length; i++)
            {
                slots[i] = _slots[ids[i]];
                if (slots[i].TryBeginRescan()) continue;
                for (var j = 0; j < i; j++) slots[j].EndRescan();
                throw new HashRefusedException(HashAlreadyRunning(ids[i]));
            }
        }

        try
        {
            return await Task.Run(() => HashCoreAsync(ids, slots, selection, progress, cancellationToken),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            foreach (var slot in slots) slot.EndRescan();
        }
    }

    private async Task<HashResult> HashCoreAsync(int[] ids, CatalogSlot[] slots, HashSelection selection,
        IProgress<HashProgress> progress, CancellationToken ct)
    {
        for (var i = 0; i < ids.Length; i++)
        {
            var refusal = CheckHashable(ids[i], slots[i]);
            if (refusal != null) throw new HashRefusedException(refusal);
        }

        var files = slots.Select(s => s.SourcePath).ToArray();
        var indexOfFile = new Dictionary<string, int>(PathComparer);
        for (var i = 0; i < files.Length; i++) indexOfFile[files[i]] = i;

        var engine = NewHashEngine();
        var pastLastCancelPoint = CancellationToken.None;
        var reporter = new HashReporter(engine, progress);
        long filesHashed;
        try
        {
            using (new Timer(_ => reporter.Tick(), null, HashProgressIntervalMs, HashProgressIntervalMs))
            {
                filesHashed = await ColumnarCatalogHashing.HashAndSave(engine, files,
                    file => reporter.Enter("saving", file), output: null, selection, ct,
                    replace: (staged, file) =>
                    {
                        reporter.Enter("swapping", file);
                        var i = indexOfFile[file];
                        Swap(slots[i], ids[i], staged, file, legacy: false, pastLastCancelPoint);
                    }).ConfigureAwait(false);
            }
        }
        finally
        {
            reporter.End();
            foreach (var file in files) TryDelete(file + ".tmp");
        }

        return new HashResult { FilesHashed = filesHashed, FilesFailed = engine.FilesFailed };
    }

    private Duplication NewHashEngine()
    {
        var engine = new Duplication(new OffConsoleEngineLog(_logger), new EngineConfig(_hashingConfig?.Invoke()),
            new ApplicationDiagnostics(), new OperationCancellation());
        engine.StatusMessageEvent = message => _logger?.Information("Hash: {Message}", message);
        return engine;
    }

    private static JobError ParseHashRequest(IReadOnlyList<int> catalogIds, HashOptions options, out int[] ids,
        out HashSelection selection)
    {
        ids = null;
        selection = null;
        if (catalogIds is not { Count: > 0 })
            return HashRefusal(JobErrorCodes.InvalidRequest, "A hash needs at least one catalog in catalogIds.");

        try
        {
            selection = HashSelection.Parse(options?.Filter, options?.IncludeUniqueSizes ?? false);
        }
        catch (EverythingQueryException ex)
        {
            return HashRefusal(JobErrorCodes.InvalidFilter, ex.Message);
        }

        ids = catalogIds.Distinct().Order().ToArray();
        return null;
    }

    private static JobError CheckHashable(int id, CatalogSlot slot)
    {
        if (!slot.TryEnterRead(out var lease)) return HashUnavailable(id);
        string rootPath;
        using (lease)
        {
            if (lease.Source is not ColumnarCatalogReader) return LegacyHashCatalog(id);
            rootPath = lease.Source.RootPath;
        }

        if (string.IsNullOrEmpty(slot.SourcePath))
            return HashRefusal(JobErrorCodes.NoSourceFile, $"Catalog {id} has no source file to save hashes to.");
        return IsRootAccessible(rootPath)
            ? null
            : new JobError
            {
                Code = JobErrorCodes.RootNotAccessible,
                Message = new RootNotAccessibleException(rootPath).Message,
                RootPath = rootPath
            };
    }

    private static JobError HashRefusal(string code, string message) => new() { Code = code, Message = message };

    private static JobError HashBusy() =>
        HashRefusal(JobErrorCodes.Busy, "Catalogs are being loaded or migrated; try again when that finishes.");

    private static JobError UnknownHashCatalog(int id) => HashRefusal(JobErrorCodes.UnknownCatalog, $"No catalog {id}.");

    private static JobError HashAlreadyRunning(int id) =>
        HashRefusal(JobErrorCodes.AlreadyRunning, $"A job for catalog {id} is already queued or running.");

    private static JobError HashUnavailable(int id) =>
        HashRefusal(JobErrorCodes.CatalogUnavailable, $"Catalog {id} is unavailable after a failed rescan; reload catalogs.");

    private static JobError LegacyHashCatalog(int id) =>
        HashRefusal(JobErrorCodes.LegacyCatalog, $"Catalog {id} is a legacy .cde catalog, which can't be hashed; migrate it to .cdex first.");

    private sealed class HashReporter(Duplication engine, IProgress<HashProgress> progress)
    {
        private readonly object _lock = new();
        private bool _pastHashing;

        public void Tick()
        {
            lock (_lock)
            {
                if (_pastHashing) return;
                var p = engine.Progress;
                progress?.Report(new HashProgress(p.FullPass ? "hashing-full" : "hashing", Counts(p), null));
            }
        }

        public void Enter(string phase, string file)
        {
            lock (_lock)
            {
                _pastHashing = true;
                progress?.Report(new HashProgress(phase, Counts(engine.Progress), file));
            }
        }

        public void End()
        {
            lock (_lock) _pastHashing = true;
        }

        private static HashJobProgress Counts(HashRunProgress p) =>
            new(p.FilesDone, p.FilesTotal, p.BytesDone, p.BytesTotal);
    }

    private sealed class OffConsoleEngineLog(Serilog.ILogger logger) : ILogger
    {
        public void LogException(Exception ex, string format, params object[] paramList) =>
            logger?.Error(ex, format, paramList);

        public void LogInfo(string format, params object[] paramList) => logger?.Debug(format, paramList);

        public void LogDebug(string format, params object[] paramList) => logger?.Debug(format, paramList);
    }

    private sealed class EngineConfig(HashingSection hashing) : IConfiguration
    {
        public AppConfigurationSection Config { get; } = new() { Hashing = hashing ?? new HashingSection() };
        public int ProgressUpdateInterval => Config.Display.ProgressUpdateInterval;
        public int HashFirstPassSize => Config.Hashing.FirstPassSizeInBytes;
        public int DegreesOfParallelism => Config.Hashing.DegreesOfParallelism;
    }
}
