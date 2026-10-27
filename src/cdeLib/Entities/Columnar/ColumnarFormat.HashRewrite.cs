using System;
using System.IO;
using System.Runtime.InteropServices;

namespace cdeLib.Entities.Columnar;

/// <summary>New flags and hash for one entry of a catalog, by entry index.</summary>
public readonly record struct EntryHashUpdate(int Index, Flags Flags, Hash16 Hash);

public static partial class ColumnarFormat
{
    private const int RewriteChunk = 64 * 1024;

    /// <summary>
    /// Writes a copy of <paramref name="source"/> with new flags and hashes for the entries in
    /// <paramref name="updates"/> (sorted by ascending index), without building a tree (issue #40). Every
    /// other column is copied as it is. The output is byte-identical to rebuilding the tree, setting the same
    /// hashes and writing it: the hash column is present only when some entry has a usable hash, an entry
    /// without a usable hash gets a zero hash, and the metadata records <paramref name="actualFileName"/>.
    /// Writes straight to <paramref name="outPath"/>; the caller owns any temp file and swap (the source
    /// file is still mapped while this runs).
    /// </summary>
    public static void WriteWithHashes(ColumnarCatalogReader source, ReadOnlySpan<EntryHashUpdate> updates,
        string actualFileName, string outPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (var u = 0; u < updates.Length; u++)
        {
            if ((uint)updates[u].Index >= (uint)source.Count || (u > 0 && updates[u].Index <= updates[u - 1].Index))
            {
                throw new ArgumentException("Updates must be in ascending, unique entry index order within the catalog.",
                    nameof(updates));
            }
        }

        var count = source.Count;
        var sourceBits = source.BitFields;

        // The tree writer stores a hash column only when a non-root entry has a usable hash.
        var hasHashes = false;
        for (int i = 1, u = 0; i < count && !hasHashes; i++)
        {
            var flags = (Flags)sourceBits[i];
            while (u < updates.Length && updates[u].Index < i) u++;
            if (u < updates.Length && updates[u].Index == i) flags = updates[u].Flags;
            hasHashes = flags.IsHashUsable();
        }

        var meta = BuildMeta(
            source.RootPath, source.VolumeName, source.DefaultFileName, actualFileName, source.DriveLetterHint,
            source.Description, source.AvailSpace, source.TotalSpace, source.ScanStartUtcTicks, source.ScanEndUtcTicks,
            source.RootSize, source.RootFileEntryCount, source.RootDirEntryCount);

        var len = new long[ColumnCount];
        for (var c = 0; c < ColumnCount; c++) len[c] = source.ColumnBytes((Col)c).Length;
        len[(int)Col.Hash] = hasHashes ? (long)count * 16 : 0;
        len[(int)Col.Meta] = meta.Length;

        var off = LayOut(len, out var pos);
        var header = BuildHeader(count, ComposeFlags(hasHashes, source.FollowJunctions), off, len);

        using var handle = File.OpenHandle(outPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
        RandomAccess.SetLength(handle, pos); // gap bytes between columns stay zero
        RandomAccess.Write(handle, header, 0);

        for (var c = 0; c < ColumnCount; c++)
        {
            var col = (Col)c;
            if (col is Col.BitFields or Col.Hash or Col.Meta) continue;
            RandomAccess.Write(handle, source.ColumnBytes(col), off[c]);
        }

        RandomAccess.Write(handle, meta, off[(int)Col.Meta]);

        // Bit fields and hashes are patched in chunks so neither column is held whole in memory.
        var bitsChunk = new byte[RewriteChunk];
        var hashChunk = hasHashes ? new Hash16[RewriteChunk] : null;
        var nextUpdate = 0;
        for (var start = 0; start < count; start += RewriteChunk)
        {
            var n = Math.Min(RewriteChunk, count - start);
            for (var k = 0; k < n; k++)
            {
                var i = start + k;
                var flags = (Flags)sourceBits[i];
                var hash = default(Hash16);
                if (nextUpdate < updates.Length && updates[nextUpdate].Index == i)
                {
                    flags = updates[nextUpdate].Flags;
                    hash = updates[nextUpdate].Hash;
                    nextUpdate++;
                }
                else if (hashChunk != null && i > 0 && flags.IsHashUsable())
                {
                    hash = source.HashOf(i);
                }

                bitsChunk[k] = (byte)flags;
                if (hashChunk != null) hashChunk[k] = i > 0 && flags.IsHashUsable() ? hash : default;
            }

            RandomAccess.Write(handle, bitsChunk.AsSpan(0, n), off[(int)Col.BitFields] + start);
            if (hashChunk != null)
            {
                RandomAccess.Write(handle, MemoryMarshal.AsBytes(hashChunk.AsSpan(0, n)), off[(int)Col.Hash] + (long)start * 16);
            }
        }
    }
}
