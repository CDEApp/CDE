using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeLib.Entities.Columnar;

namespace cdeAppCore.Session;

public sealed partial class CatalogSession
{
    public async Task SetDescriptionAsync(int catalogId, string description, CancellationToken cancellationToken = default)
    {
        var text = (description ?? "").Trim();
        if (text.Length > ICatalogSession.MaxDescriptionLength)
            throw new ArgumentException($"A description is at most {ICatalogSession.MaxDescriptionLength} characters.", nameof(description));

        CatalogSlot slot;
        lock (_gate)
        {
            if (_loading || _migrating)
                throw new InvalidOperationException("Cannot edit a description while catalogs are being loaded or migrated.");
            if (catalogId < 0 || catalogId >= _slots.Count) throw new ArgumentOutOfRangeException(nameof(catalogId));
            slot = _slots[catalogId];
            // The same one-job-per-catalog gate a rescan uses: both end in a file swap.
            if (!slot.TryBeginRescan()) throw new RescanAlreadyRunningException(catalogId);
        }

        try
        {
            await Task.Run(() => SetDescriptionCore(slot, catalogId, text, cancellationToken), CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            slot.EndRescan();
        }
    }

    private void SetDescriptionCore(CatalogSlot slot, int catalogId, string description, CancellationToken ct)
    {
        var target = slot.SourcePath;
        if (string.IsNullOrEmpty(target)) throw new NoSourceFileException(catalogId);
        var tmp = target + ".tmp";

        try
        {
            if (!slot.TryEnterRead(out var lease)) throw new InvalidOperationException($"Catalog {catalogId} is unavailable; reload.");
            using (lease)
            {
                if (lease.Source is not ColumnarCatalogReader columnar)
                    throw new NotSupportedException("A legacy .cde catalog can't be edited here; migrate it to .cdex first.");
                if (string.Equals(columnar.Description ?? "", description, StringComparison.Ordinal)) return;
                ColumnarFormat.WriteWithDescription(columnar, description, tmp);
            }

            ct.ThrowIfCancellationRequested();
            Swap(slot, catalogId, tmp, target, legacy: false, ct);
        }
        finally
        {
            TryDelete(tmp);
        }
    }
}
