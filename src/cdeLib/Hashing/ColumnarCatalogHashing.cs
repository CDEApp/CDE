using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using cdeLib.Duplicates;
using cdeLib.Entities.Columnar;

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
    public static async Task HashAndSave(Duplication duplication, IList<string> cdexFiles, Action<string> onSaving)
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

            for (var c = 0; c < readers.Count; c++)
            {
                var file = cdexFiles[c];
                onSaving?.Invoke(file);
                var tempPath = file + ".tmp";
                try
                {
                    ColumnarFormat.WriteWithHashes(readers[c], updates.For(c), file, tempPath);
                    readers[c].Dispose(); // release the mapping so the file can be replaced
                    File.Move(tempPath, file, overwrite: true);
                }
                catch
                {
                    try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
                    throw;
                }
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
}
