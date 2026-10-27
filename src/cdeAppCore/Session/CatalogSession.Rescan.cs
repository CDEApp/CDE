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
        lock (_gate)
        {
            if (_loading) return RescanPreflight.Busy; // the slot list is being rebuilt: ids mean nothing yet
        }

        if (catalogId < 0 || catalogId >= _slots.Count) return RescanPreflight.UnknownCatalog;
        var slot = _slots[catalogId];
        if (slot.IsRescanning) return RescanPreflight.AlreadyRunning;
        if (string.IsNullOrEmpty(slot.SourcePath)) return RescanPreflight.NoSourceFile;
        if (!slot.TryEnterRead(out var lease)) return RescanPreflight.Unavailable;
        string rootPath;
        using (lease) rootPath = lease.Source.RootPath;
        return IsRootAccessible(rootPath) ? RescanPreflight.Ready : RescanPreflight.RootNotAccessible;
    }

    public async Task<RescanResult> RescanAsync(int catalogId, IProgress<RescanProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        CatalogSlot slot;
        lock (_gate)
        {
            if (_loading) throw new InvalidOperationException("Cannot rescan while catalogs are being loaded.");
            if (catalogId < 0 || catalogId >= _slots.Count) throw new ArgumentOutOfRangeException(nameof(catalogId));
            slot = _slots[catalogId];
            if (!slot.TryBeginRescan()) throw new RescanAlreadyRunningException(catalogId);
        }

        try
        {
            // The scan is synchronous file-system work; keep it off the caller's thread.
            return await Task.Run(() => RescanCore(slot, catalogId, progress, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is not OperationCanceledException)
        {
            // A cancel raised in the scan callback can arrive wrapped (traversal workers); the swap restores the
            // old files on any failure, so this is a cancellation, not a rescan failure.
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            slot.EndRescan();
        }
    }

    private RescanResult RescanCore(CatalogSlot slot, int catalogId, IProgress<RescanProgress> progress,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(slot.SourcePath)) throw new NoSourceFileException(catalogId);

        // One lease for everything we need to know about the current catalog.
        string rootPath, description;
        bool legacy, followJunctions;
        if (!slot.TryEnterRead(out var lease)) throw new InvalidOperationException($"Catalog {catalogId} is unavailable; reload.");
        using (lease)
        {
            rootPath = lease.Source.RootPath;
            description = lease.Source.Description;
            legacy = lease.Source is not ColumnarCatalogReader; // legacy .cde: reads FollowJunctions as false
            followJunctions = lease.Source.FollowJunctions;     // the scan option stored with the catalog (#66)
        }

        if (!IsRootAccessible(rootPath)) throw new RootNotAccessibleException(rootPath);

        // Legacy .cde: the new catalog is a .cdex beside it (the old file becomes .cde.bak at swap).
        var target = legacy ? Path.ChangeExtension(slot.SourcePath, ".cdex") : slot.SourcePath;
        var tmp = target + ".tmp";

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
            tree.PopulateRoot(rootPath, followJunctions, ct);
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
    /// <c>.cde</c> to <c>.cde.bak</c>), move any existing target aside to <c>.old</c>, rename the tmp over
    /// the target, map the new file, and only then delete the aside copy. Any failure restores the
    /// previous files and re-opens the old catalog.
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
            var aside = target + ".old";
            var renamedToBak = false;
            var movedAside = false;

            // Windows cannot rename over a memory-mapped file: release this catalog's mapping first.
            if (oldSource is ColumnarCatalogReader mapped) mapped.Dispose();

            try
            {
                if (legacy && File.Exists(oldPath))
                {
                    File.Move(oldPath, bak, overwrite: true);
                    renamedToBak = true;
                }

                if (File.Exists(target))
                {
                    File.Move(target, aside, overwrite: true);
                    movedAside = true;
                }

                File.Move(tmp, target);
                var fresh = _openReader(target);
                ReplaceSource(slot, catalogId, fresh, target);
            }
            catch
            {
                // Put the old files back, then the old catalog in service.
                if (movedAside)
                {
                    TryDelete(target); // the new (possibly bad) file
                    TryMove(aside, target);
                }

                if (renamedToBak && !File.Exists(oldPath)) TryMove(bak, oldPath);
                if (oldSource is ColumnarCatalogReader)
                {
                    // If even re-opening fails the catalog is unavailable (reads throw for that catalog only,
                    // listings skip it) rather than pointing at a released mapping.
                    try { ReplaceSource(slot, catalogId, _openReader(oldPath), oldPath); }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, "Could not re-open {File} after a failed swap", oldPath);
                        slot.Replace(null, oldPath);
                    }
                }

                throw;
            }

            if (movedAside) TryDelete(aside); // swap succeeded: the previous file is no longer needed
        }
        finally
        {
            slot.ExitWrite();
        }
    }

    // Install a source and rebuild that catalog's root EntryRef so Roots never wraps a disposed reader.
    private void ReplaceSource(CatalogSlot slot, int catalogId, IEntrySource source, string path)
    {
        slot.Replace(source, path);
        // Under the gate: a create appending a catalog rebuilds _roots too, and one must not lose the other's update.
        lock (_gate)
        {
            _roots = _roots.Select((r, i) => i == catalogId ? new EntryRef(source, 0) : r).ToList();
        }
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
