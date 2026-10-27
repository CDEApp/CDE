using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Session;

public sealed partial class CatalogSession
{
    public bool AnyRescanning => _slots.Any(s => s.IsRescanning);

    public bool IsRescanning(int catalogId) =>
        catalogId >= 0 && catalogId < _slots.Count && _slots[catalogId].IsRescanning;

    public RescanPreflight PreflightRescan(int catalogId)
    {
        if (catalogId < 0 || catalogId >= _slots.Count) return RescanPreflight.UnknownCatalog;
        var slot = _slots[catalogId];
        if (slot.IsRescanning) return RescanPreflight.AlreadyRunning;
        return IsRootAccessible(RootPathOf(slot)) ? RescanPreflight.Ready : RescanPreflight.RootNotAccessible;
    }

    // TODO(#66): read the catalog's stored FollowJunctions setting here. Until it is stored in the
    // .cdex (and exposed on CatalogInfoDto), a rescan never follows junctions.
    private static bool FollowJunctionsFor(CatalogSlot slot) => false;

    public async Task<RescanResult> RescanAsync(int catalogId, IProgress<RescanProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (catalogId < 0 || catalogId >= _slots.Count) throw new ArgumentOutOfRangeException(nameof(catalogId));
        var slot = _slots[catalogId];
        if (!slot.TryBeginRescan()) throw new RescanAlreadyRunningException(catalogId);
        try
        {
            // The scan is synchronous file-system work; keep it off the caller's thread.
            return await Task.Run(() => RescanCore(slot, catalogId, progress, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            slot.EndRescan();
        }
    }

    private RescanResult RescanCore(CatalogSlot slot, int catalogId, IProgress<RescanProgress> progress,
        CancellationToken ct)
    {
        var rootPath = RootPathOf(slot);
        if (!IsRootAccessible(rootPath)) throw new RootNotAccessibleException(rootPath);
        if (string.IsNullOrEmpty(slot.SourcePath))
            throw new InvalidOperationException($"Catalog {catalogId} has no source file to save to.");

        // Legacy .cde: the new catalog is a .cdex beside it (the old file becomes .cde.bak at swap).
        var legacy = !SourceIsColumnar(slot);
        var target = legacy ? Path.ChangeExtension(slot.SourcePath, ".cdex") : slot.SourcePath;
        var tmp = target + ".tmp";
        var description = DescriptionOf(slot);

        try
        {
            progress?.Report(new RescanProgress(catalogId, "scanning", 0, rootPath));
            var tree = new RootEntry
            {
                SimpleScanCountEvent = (count, current) =>
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(new RescanProgress(catalogId, "scanning", count, current));
                }
            };
            tree.PopulateRoot(rootPath, FollowJunctionsFor(slot), ct);
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(rootPath)) throw new RootNotAccessibleException(rootPath); // vanished mid-scan

            CarryHashesForward(slot, tree, ct);
            tree.SortAllChildrenByPath();
            tree.SetSummaryFields();
            tree.Description = description;
            tree.ActualFileName = target;

            progress?.Report(new RescanProgress(catalogId, "writing", 0, rootPath));
            ColumnarFormat.WriteDirect(tree, tmp);
            ct.ThrowIfCancellationRequested();

            progress?.Report(new RescanProgress(catalogId, "swapping", 0, rootPath));
            Swap(slot, catalogId, tmp, target, legacy, ct);

            return new RescanResult
            {
                CatalogId = catalogId,
                RootPath = rootPath,
                Entries = (long)tree.FileEntryCount + tree.DirEntryCount,
                SavedPath = target
            };
        }
        finally
        {
            // After a successful swap the .tmp has been renamed away; on any other exit it is removed.
            TryDelete(tmp);
        }
    }

    // Hash reuse reads the old catalog under a read lock (the swap cannot run while we use its mapping).
    private void CarryHashesForward(CatalogSlot slot, RootEntry fresh, CancellationToken ct)
    {
        using var lease = slot.EnterRead();
        if (!string.Equals(lease.Source.RootPath, fresh.Path, StringComparison.OrdinalIgnoreCase)) return;

        if (lease.Source is ColumnarCatalogReader columnar)
        {
            ColumnarHashReuse.CopyHashes(columnar, fresh);
            return;
        }

        // Legacy in-memory store: round-trip it through a temporary .cdex so the same reuse rules apply.
        if (lease.Source is not EntryStore store) return;
        var scratch = Path.Combine(Path.GetTempPath(), $"cde-rescan-old-{Guid.NewGuid():N}.cdex");
        try
        {
            ct.ThrowIfCancellationRequested();
            ColumnarFormat.Write(store, scratch);
            using var old = new ColumnarCatalogReader(scratch);
            ColumnarHashReuse.CopyHashes(old, fresh);
        }
        finally
        {
            TryDelete(scratch);
        }
    }

    /// <summary>
    /// Replace the slot's source with the file at <paramref name="tmp"/>. Takes the write lock (waits for
    /// running searches/listings), then, in this order: release the old mapping, (legacy: rename old
    /// <c>.cde</c> to <c>.cde.bak</c>), rename the tmp over the target, map the new file. Any failure
    /// puts the old file name and mapping back.
    /// </summary>
    private void Swap(CatalogSlot slot, int catalogId, string tmp, string target, bool legacy, CancellationToken ct)
    {
        slot.EnterWrite(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var oldSource = slot.Source;
            var oldPath = slot.SourcePath;
            var bak = legacy ? oldPath + ".bak" : null;
            var renamedToBak = false;

            // Windows cannot rename over a memory-mapped file: release this catalog's mapping first.
            if (oldSource is ColumnarCatalogReader mapped) mapped.Dispose();

            try
            {
                if (legacy && File.Exists(oldPath))
                {
                    File.Move(oldPath, bak, overwrite: true);
                    renamedToBak = true;
                }

                File.Move(tmp, target, overwrite: true);
                var fresh = new ColumnarCatalogReader(target);
                slot.Replace(fresh, target);
            }
            catch
            {
                if (renamedToBak && !File.Exists(oldPath)) TryMove(bak, oldPath);
                if (oldSource is ColumnarCatalogReader)
                {
                    // Put the old catalog back in service. If even that fails the catalog is unavailable
                    // (reads throw) rather than pointing at a released mapping.
                    try { slot.Replace(new ColumnarCatalogReader(oldPath), oldPath); }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, "Could not re-open {File} after a failed swap", oldPath);
                        slot.Replace(null, oldPath);
                    }
                }

                throw;
            }

            _roots = _roots.Select((r, i) => i == catalogId ? new EntryRef(slot.Source, 0) : r).ToList();
        }
        finally
        {
            slot.ExitWrite();
        }
    }

    private static string RootPathOf(CatalogSlot slot)
    {
        using var lease = slot.EnterRead();
        return lease.Source.RootPath;
    }

    private static string DescriptionOf(CatalogSlot slot)
    {
        using var lease = slot.EnterRead();
        return lease.Source.Description;
    }

    private static bool SourceIsColumnar(CatalogSlot slot)
    {
        using var lease = slot.EnterRead();
        return lease.Source is ColumnarCatalogReader;
    }

    // Exists and can be listed: a drive that is unplugged, a moved folder or a denied folder all fail here.
    private static bool IsRootAccessible(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) return false;
        try
        {
            if (!Directory.Exists(rootPath)) return false;
            using var e = Directory.EnumerateFileSystemEntries(rootPath).GetEnumerator();
            e.MoveNext();
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException
                                       or NotSupportedException)
        {
            return false;
        }
    }

    // Retries briefly: a virus scanner or indexer can hold a just-written file for a moment on Windows.
    private static void TryDelete(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            catch (Exception) when (attempt < 19)
            {
                Thread.Sleep(100);
            }
            catch (Exception)
            {
                return; // best effort
            }
        }
    }

    private static void TryMove(string from, string to)
    {
        try { File.Move(from, to); }
        catch (Exception) { /* best effort */ }
    }
}
