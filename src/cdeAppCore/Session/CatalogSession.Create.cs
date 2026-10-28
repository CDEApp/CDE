using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Session;

// "Add" alongside the rescan's "replace": a new catalog is appended to the session under the same _gate that
// serialises loads and rescans, so it can't race either of them. Ids are slot indexes and slots are only ever
// appended, so existing CatalogIds never change.
public sealed partial class CatalogSession
{
    // The folder catalogs were last loaded from: where new catalogs are written. Guarded by _gate.
    private string _catalogDir;

    // Number of creates running, and the full target paths they are about to write. Guarded by _gate.
    private int _creating;
    private readonly HashSet<string> _inFlightTargets = new(PathComparer);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string CatalogDir
    {
        get { lock (_gate) return _catalogDir; }
    }

    public bool AnyCatalogJobRunning
    {
        get
        {
            lock (_gate) return _creating > 0 || _migrating || AnyRescanning;
        }
    }

    // Called by LoadAsync with the gate held.
    private void RememberCatalogDir(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath)) return;
        try { _catalogDir = Path.GetFullPath(configPath); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Not a usable folder: keep the previous one.
        }
    }

    public CreatePreflightResult PreflightCreate(CreatePreflightRequest request) => PreflightCore(request, null);

    // `ownReservation` is the in-flight target of the create asking, which must not count against itself.
    private CreatePreflightResult PreflightCore(CreatePreflightRequest request, string ownReservation)
    {
        var rootPath = request?.RootPath;
        var defaultName = CatalogNaming.DefaultFileName(rootPath);
        var typed = CatalogNaming.NormalizeFileName(request?.FileName);
        var fileName = typed.Length > 0 ? typed : defaultName ?? "";
        var nameError = CatalogNaming.ValidateFileName(fileName);

        // One consistent view of the session state: busy, working folder, and whether the target is being written.
        bool busy;
        string dir;
        string target = null;
        var inFlight = false;
        lock (_gate)
        {
            busy = _loading || _migrating;
            dir = _catalogDir;
            if (dir != null && nameError == null)
            {
                target = Path.Combine(dir, fileName + CatalogNaming.Extension);
                inFlight = _inFlightTargets.Contains(target) && !PathComparer.Equals(target, ownReservation);
            }
        }

        var canonical = CatalogNaming.CanonicalRoot(rootPath);
        var result = new CreatePreflightResult
        {
            Busy = busy,
            DefaultFileName = defaultName,
            FileName = fileName,
            FileNameError = nameError,
            CatalogDir = dir,
            TargetPath = target,
            RootAccessible = canonical != null && IsRootAccessible(canonical),
            Writable = IsWritable(dir),
            SameRootCatalogIds = SameRootCatalogs(canonical)
        };

        if (target != null)
        {
            result.Exists = inFlight || File.Exists(target);
            if (result.Exists) result.ExistingCatalogId = CatalogIdForFile(target);
        }

        return result;
    }

    private int[] SameRootCatalogs(string canonicalRoot)
    {
        if (canonicalRoot == null) return [];
        var slots = _slots;
        var ids = new List<int>();
        for (var i = 0; i < slots.Count; i++)
        {
            if (!slots[i].TryEnterRead(out var lease)) continue;
            string root;
            using (lease) root = lease.Source.RootPath;
            if (CatalogNaming.SameRoot(root, canonicalRoot)) ids.Add(i);
        }

        return ids.ToArray();
    }

    private int? CatalogIdForFile(string path)
    {
        var slots = _slots;
        for (var i = 0; i < slots.Count; i++)
        {
            var sp = slots[i].SourcePath;
            if (sp != null && PathComparer.Equals(Path.GetFullPath(sp), Path.GetFullPath(path))) return i;
        }

        return null;
    }

    // Can a file be created in the folder? Probes with a self-deleting file; never creates the folder.
    private static bool IsWritable(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return false;
        try
        {
            if (!Directory.Exists(dir)) return false;
            var probe = Path.Combine(dir, $".cde-write-test-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                       FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException
                                       or NotSupportedException)
        {
            return false;
        }
    }

    private static void ThrowIfBlocked(CreatePreflightResult pf, string rootPath)
    {
        if (pf.FileNameError != null) throw new InvalidCatalogFileNameException(pf.FileNameError);
        if (pf.CatalogDir == null) throw new NoCatalogDirException();
        if (!pf.RootAccessible) throw new RootNotAccessibleException(rootPath);
        if (!pf.Writable) throw new CatalogFolderNotWritableException(pf.CatalogDir);
        if (pf.Exists) throw new CatalogFileExistsException(pf.TargetPath, pf.ExistingCatalogId);
    }

    public async Task<CreateCatalogResult> CreateCatalogAsync(CreateCatalogRequest request,
        IProgress<CreateProgress> progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = CatalogNaming.NormalizeFileName(request.FileName);
        if (name.Length == 0) name = CatalogNaming.DefaultFileName(request.RootPath) ?? "";
        var nameError = CatalogNaming.ValidateFileName(name);
        if (nameError != null) throw new InvalidCatalogFileNameException(nameError);

        string target;
        lock (_gate)
        {
            if (_loading || _migrating)
                throw new CatalogJobBusyException("Cannot create a catalog while catalogs are being loaded or migrated.");
            if (_catalogDir == null) throw new NoCatalogDirException();
            target = Path.Combine(_catalogDir, name + CatalogNaming.Extension);
            if (_inFlightTargets.Contains(target)) throw new CatalogFileExistsException(target, null);
            _inFlightTargets.Add(target);
            _creating++;
        }

        try
        {
            var pf = PreflightCore(new CreatePreflightRequest { RootPath = request.RootPath, FileName = name }, target);
            ThrowIfBlocked(pf, request.RootPath);

            // The scan is synchronous file-system work; keep it off the caller's thread.
            return await Task.Run(() => CreateCore(request, target, progress, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is not OperationCanceledException)
        {
            // The scan callback runs on traversal workers, so a cancel can arrive wrapped. Nothing was committed
            // (a commit returns normally), so report it as the cancellation it is, not as a failure.
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            lock (_gate)
            {
                _creating--;
                _inFlightTargets.Remove(target);
            }
        }
    }

    private CreateCatalogResult CreateCore(CreateCatalogRequest request, string target, IProgress<CreateProgress> progress,
        CancellationToken ct)
    {
        var tmp = target + ".tmp";
        try
        {
            progress?.Report(new CreateProgress("scanning", 0, request.RootPath));
            var tree = new RootEntry
            {
                SimpleScanCountEvent = (count, current) =>
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(new CreateProgress("scanning", count, current));
                }
            };
            tree.PopulateRoot(request.RootPath, request.FollowJunctions, ct);
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(tree.Path)) throw new RootNotAccessibleException(tree.Path); // vanished mid-scan

            tree.SortAllChildrenByPath();
            tree.SetSummaryFields();
            if (!string.IsNullOrWhiteSpace(request.Description)) tree.Description = request.Description.Trim();
            tree.ActualFileName = target;

            progress?.Report(new CreateProgress("writing", 0, tree.Path));
            ColumnarFormat.WriteDirect(tree, tmp);
            ct.ThrowIfCancellationRequested();

            // Last chance to cancel: after the rename the catalog exists and is added.
            progress?.Report(new CreateProgress("saving", 0, tree.Path));
            ct.ThrowIfCancellationRequested();

            // No overwrite, checked by the file system at rename time (covers a file appearing since the preflight).
            try
            {
                File.Move(tmp, target, overwrite: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                throw new CatalogFileExistsException(target, CatalogIdForFile(target));
            }

            int catalogId;
            try
            {
                var reader = _openReader(target);
                lock (_gate)
                {
                    var slot = new CatalogSlot(reader, target);
                    // Copy-on-write so lock-free readers never see a list change under them.
                    _slots = [.. _slots, slot];
                    _roots = [.. _roots, new EntryRef(reader, 0)];
                    catalogId = _slots.Count - 1;
                }
            }
            catch
            {
                TryDelete(target); // we just created it: leave nothing behind
                throw;
            }

            return new CreateCatalogResult
            {
                CatalogId = catalogId,
                RootPath = tree.Path,
                Entries = (long)tree.FileEntryCount + tree.DirEntryCount,
                SavedPath = target
            };
        }
        finally
        {
            TryDelete(tmp); // gone already after a successful rename
        }
    }
}
