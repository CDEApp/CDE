using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Output;

namespace cdeLib.Hashing;

/// <summary>
/// <c>cde hash</c> over columnar catalogs (issue #40): hashes straight from the memory-mapped readers with
/// <see cref="Duplication.ApplyHash(IReadOnlyList{ColumnarCatalogReader})"/>, then rewrites each catalog with
/// its new hashes. No pointer tree is built. Each catalog is written beside itself and moved into place once its
/// mapping is released, so a failure part way never destroys a good catalog.
/// </summary>
public static class ColumnarCatalogHashing
{
    /// <param name="duplication">The hashing engine (its progress and status callbacks are used as set).</param>
    /// <param name="cdexFiles">The catalogs to hash; each is replaced by its hashed copy.</param>
    /// <param name="onSaving">Called with each catalog's path just before it is saved.</param>
    /// <param name="output">Receives a record for each file hashed in this run, with its new hash, once the catalog it is in
    /// has saved (#173); a catalog that fails to save writes none. Null writes none. Files whose usable hash was already in the catalog are not repeated.</param>
    public static async Task HashAndSave(Duplication duplication, IList<string> cdexFiles, Action<string> onSaving,
        IRecordWriter output = null)
    {
        ArgumentNullException.ThrowIfNull(duplication);
        ArgumentNullException.ThrowIfNull(cdexFiles);

        var readers = new List<ColumnarCatalogReader>(cdexFiles.Count);
        try
        {
            foreach (var file in cdexFiles)
            {
                readers.Add(new ColumnarCatalogReader(file));
            }

            var updates = await duplication.ApplyHash(readers).ConfigureAwait(false);

            // A catalog that fails to save does not stop the others: their hashes are expensive to redo.
            List<Exception> failures = null;
            for (var c = 0; c < readers.Count; c++)
            {
                var file = cdexFiles[c];
                onSaving?.Invoke(file);
                var tempPath = file + ".tmp";
                var saved = false;
                try
                {
                    ColumnarFormat.WriteWithHashes(readers[c], updates.For(c), file, tempPath);
                    readers[c].Dispose(); // release the mapping so the file can be replaced
                    File.Move(tempPath, file, overwrite: true);
                    saved = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    TryDelete(tempPath);
                    (failures ??= []).Add(ex);
                }

                // Only a catalog that saved has its records written: a record says a hash is in the catalog.
                if (saved && output != null) WriteHashed(output, file, updates.For(c));
            }

            // The completed hashes are saved above even when a phase failed; now surface the failure.
            var phaseFailure = duplication.UnexpectedFailure;
            if (failures != null || phaseFailure != null)
            {
                var reasons = new List<string>();
                if (phaseFailure != null) reasons.Add($"unexpected error: {phaseFailure.Message}");
                if (failures != null)
                    reasons.Add(failures.Count == 1
                        ? $"failed to save a hashed catalog: {failures[0].Message}"
                        : $"failed to save {failures.Count} hashed catalogs");
                var all = new List<Exception>();
                if (phaseFailure != null) all.Add(phaseFailure);
                if (failures != null) all.AddRange(failures);
                throw new HashFailedException(
                    HashFailedException.Summarize(duplication.FilesHashed, duplication.FilesFailed,
                        string.Join("; ", reasons)),
                    all.Count == 1 ? all[0] : new AggregateException(all));
            }
        }
        finally
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }
        }
    }

    // Streams one record per newly hashed file of a saved catalog, straight from the updates, so nothing is collected in
    // memory. The saved catalog has the same entries in the same order, so the update indexes address it too.
    private static void WriteHashed(IRecordWriter output, string file, ReadOnlySpan<EntryHashUpdate> updates)
    {
        using var saved = new ColumnarCatalogReader(file);
        foreach (var update in updates)
        {
            if (!update.Flags.IsHashUsable()) continue;
            EntryRecords.WriteHashed(output, file, saved, update.Index, update.Hash,
                (update.Flags & Flags.PartialHash) == Flags.PartialHash);
        }

        output.Flush();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort: the failure being reported matters more than a stale temp file
        }
    }
}
