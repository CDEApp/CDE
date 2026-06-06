using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using cdeLib.Entities.Soa;

namespace cdeMemProbe.Columnar;

/// <summary>
/// SPIKE — hand-rolled columnar (struct-of-arrays) on-disk catalog format, designed for
/// <b>zero-copy reads over a memory-mapped file</b>. The whole point is that "loading" a catalog
/// becomes mmap-ing the file: no managed object graph is built, so the working set is just the
/// file pages a query actually touches (in the reclaimable OS page cache), not GC heap.
///
/// Layout (all little-endian; x64 assumed for the spike):
///   preamble:
///     [0]  magic   "CDEX" (4 bytes)
///     [4]  int32   version
///     [8]  int32   count            (entries incl. root; index 0 = root)
///     [12] int32   flags            (bit0 = hasHashes)
///     [16] (int64 offset, int64 length) x <see cref="ColumnCount"/>   -- absolute, 8-aligned
///   column bodies (each padded to an 8-byte boundary), in <see cref="Col"/> order.
///
/// Columns are dense and homogeneous — a name-only search sequentially scans just the NameBlob +
/// NameOffsets columns and never pages in Size/Modified/Hash. That column-skipping is the memory
/// lever the in-memory store cannot offer.
///
/// Why hand-rolled rather than FlatBuffers/FlatSharp: for dense fixed-width columns this gives a
/// genuinely alloc-free <see cref="MemoryMarshal.Cast{T,T}"/> view straight over the mapping, with
/// no vtable indirection or per-access string materialization (FlatSharp lazy mode's main pitfall).
/// FlatBuffers earns its keep for sparse/optional schemas; a catalog is the opposite of that.
/// </summary>
public static class ColumnarFormat
{
    public static readonly byte[] Magic = "CDEX"u8.ToArray();
    public const int Version = 1;
    public const int FlagHasHashes = 1;

    /// <summary>Fixed column ordering. Hash/Meta lengths are 0 when absent.</summary>
    public enum Col
    {
        ModifiedTicks = 0, // long[count]
        Size,              // long[count]
        BitFields,         // byte[count]
        Parent,            // int[count]
        FirstChild,        // int[count]
        NextSibling,       // int[count]
        NameOffsets,       // int[count+1]  prefix offsets into NameBlob
        NameBlob,          // byte[]        UTF-8 full names (name+ext) concatenated
        Hash,              // byte[16*count] (only when hasHashes)
        Meta,              // byte[]        catalog metadata blob
    }

    public const int ColumnCount = 10;
    public const int PreambleFixed = 16;                  // magic+version+count+flags
    public const int HeaderSize = PreambleFixed + ColumnCount * 16;

    private static long Align8(long v) => (v + 7) & ~7L;

    /// <summary>Convert an in-memory <see cref="EntryStore"/> to the columnar file. One-time cost.</summary>
    public static void Write(EntryStore store, string outPath)
    {
        var count = store.Count;
        var hasHashes = store.Hash != null;

        // Build the variable-length name columns up front (UTF-8 full names + prefix offsets).
        var nameOffsets = new int[count + 1];
        using var nameBlob = new MemoryStream(count * 12);
        for (var i = 0; i < count; i++)
        {
            nameOffsets[i] = (int)nameBlob.Length;
            WriteUtf8(nameBlob, store.Name[i]);
            WriteUtf8(nameBlob, store.Ext[i]); // ext appended directly -> full name bytes, no separator
        }
        nameOffsets[count] = (int)nameBlob.Length;
        var nameBlobBytes = nameBlob.GetBuffer().AsSpan(0, (int)nameBlob.Length);

        var meta = BuildMeta(store);

        // Lengths per column.
        var len = new long[ColumnCount];
        len[(int)Col.ModifiedTicks] = (long)count * sizeof(long);
        len[(int)Col.Size] = (long)count * sizeof(long);
        len[(int)Col.BitFields] = count;
        len[(int)Col.Parent] = (long)count * sizeof(int);
        len[(int)Col.FirstChild] = (long)count * sizeof(int);
        len[(int)Col.NextSibling] = (long)count * sizeof(int);
        len[(int)Col.NameOffsets] = (long)(count + 1) * sizeof(int);
        len[(int)Col.NameBlob] = nameBlobBytes.Length;
        len[(int)Col.Hash] = hasHashes ? (long)count * 16 : 0;
        len[(int)Col.Meta] = meta.Length;

        // Offsets: header first, then each column 8-aligned.
        var off = new long[ColumnCount];
        var pos = (long)HeaderSize;
        for (var c = 0; c < ColumnCount; c++)
        {
            pos = Align8(pos);
            off[c] = pos;
            pos += len[c];
        }

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None,
            1 << 20, FileOptions.SequentialScan);

        // Preamble.
        fs.Write(Magic);
        WriteI32(fs, Version);
        WriteI32(fs, count);
        WriteI32(fs, hasHashes ? FlagHasHashes : 0);
        for (var c = 0; c < ColumnCount; c++)
        {
            WriteI64(fs, off[c]);
            WriteI64(fs, len[c]);
        }

        // Column bodies (re-pad to each column's recorded offset).
        WriteCol(fs, off[(int)Col.ModifiedTicks], MemoryMarshal.AsBytes(store.ModifiedTicks.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.Size], MemoryMarshal.AsBytes(store.Size.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.BitFields], store.BitFields.AsSpan(0, count));
        WriteCol(fs, off[(int)Col.Parent], MemoryMarshal.AsBytes(store.Parent.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.FirstChild], MemoryMarshal.AsBytes(store.FirstChild.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.NextSibling], MemoryMarshal.AsBytes(store.NextSibling.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.NameOffsets], MemoryMarshal.AsBytes(nameOffsets.AsSpan()));
        WriteCol(fs, off[(int)Col.NameBlob], nameBlobBytes);
        if (hasHashes)
            WriteCol(fs, off[(int)Col.Hash], MemoryMarshal.AsBytes(store.Hash.AsSpan(0, count)));
        WriteCol(fs, off[(int)Col.Meta], meta);
    }

    private static byte[] BuildMeta(EntryStore s)
    {
        using var ms = new MemoryStream(256);
        WriteLenString(ms, s.RootPath);
        WriteLenString(ms, s.VolumeName);
        WriteLenString(ms, s.DefaultFileName);
        WriteLenString(ms, s.ActualFileName);
        WriteLenString(ms, s.DriveLetterHint);
        WriteLenString(ms, s.Description);
        WriteI64(ms, s.AvailSpace);
        WriteI64(ms, s.TotalSpace);
        WriteI64(ms, s.ScanStartUtcTicks);
        WriteI64(ms, s.ScanEndUtcTicks);
        WriteI64(ms, s.RootSize);
        WriteI32(ms, (int)s.RootFileEntryCount);
        WriteI32(ms, (int)s.RootDirEntryCount);
        return ms.ToArray();
    }

    private static void WriteCol(FileStream fs, long offset, ReadOnlySpan<byte> body)
    {
        // Pad from current position up to the column's 8-aligned offset, then write the body.
        var pad = offset - fs.Position;
        for (var i = 0; i < pad; i++) fs.WriteByte(0);
        fs.Write(body);
    }

    private static void WriteUtf8(Stream s, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteLenString(Stream s, string value)
    {
        var bytes = string.IsNullOrEmpty(value) ? [] : Encoding.UTF8.GetBytes(value);
        WriteI32(s, bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteI32(Stream s, int v)
    {
        Span<byte> b = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b, v);
        s.Write(b);
    }

    private static void WriteI64(Stream s, long v)
    {
        Span<byte> b = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(b, v);
        s.Write(b);
    }
}
