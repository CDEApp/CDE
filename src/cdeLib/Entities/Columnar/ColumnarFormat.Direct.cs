using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace cdeLib.Entities.Columnar;

public static partial class ColumnarFormat
{
    private const int NoEntry = -1;

    /// <summary>
    /// Writes the columnar file straight from a scanned/loaded tree, without first building an
    /// <see cref="Soa.EntryStore"/> copy of it. The output is byte-identical to
    /// <c>Write(EntryStore.Build(root), outPath)</c>.
    ///
    /// Pass 1 walks the tree to size every column (entry count, name bytes, whether any hash exists)
    /// and to record each directory's first-child index (the only per-entry state kept: 4 bytes per
    /// directory). Pass 2 walks it again in the same order and streams every column through its own
    /// small buffer to its final file offset, so no column is ever held whole in memory.
    /// Entry numbering matches <see cref="Soa.EntryStore.Build"/>: root is 0, and a directory's
    /// children take consecutive indices when that directory is popped from an explicit stack.
    /// </summary>
    public static void Write(RootEntry root, string outPath)
    {
        ArgumentNullException.ThrowIfNull(root);

        // Write beside the target and move into place, so a failure part-way never destroys an
        // existing good catalog (it holds hashes that are expensive to recompute).
        var tempPath = outPath + ".tmp";
        try
        {
            WriteTo(root, tempPath);
            File.Move(tempPath, outPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Writes the catalog straight to <paramref name="outPath"/> with no temp file and no rename, for callers
    /// that manage their own temp file and swap (e.g. a rescan that must release a mapping before renaming).
    /// A failure may leave a partial file at <paramref name="outPath"/>; the caller deletes it.
    /// </summary>
    public static void WriteDirect(RootEntry root, string outPath)
    {
        ArgumentNullException.ThrowIfNull(root);
        WriteTo(root, outPath);
    }

    private static void WriteTo(RootEntry root, string outPath)
    {

        // ---- pass 1: sizes + first-child index per directory ----
        var firstChildOfDir = new List<int> { NoEntry }; // [0] = root; one slot per directory, in encounter order
        var rootNameBytes = Utf8Length(root.Path);
        var nameBytes = rootNameBytes;
        var hasHashes = false;
        var count = 1;

        var stack = new Stack<(IList<DirEntry> Kids, int DirOrdinal)>();
        stack.Push((root.Children, 0));
        while (stack.Count > 0)
        {
            var (kids, ordinal) = stack.Pop();
            if (kids == null) continue;
            if (kids.Count > 0) firstChildOfDir[ordinal] = count;
            foreach (var child in kids)
            {
                count++;
                nameBytes += Utf8Length(child.NamePart) + Utf8Length(child.ExtPart);
                if (child.IsHashDone) hasHashes = true;
                if (child.IsDirectory)
                {
                    firstChildOfDir.Add(NoEntry);
                    stack.Push((child.Children, firstChildOfDir.Count - 1));
                }
            }
        }

        var meta = BuildMeta(
            root.Path, root.VolumeName, root.DefaultFileName, root.ActualFileName, root.DriveLetterHint,
            root.Description, root.AvailSpace, root.TotalSpace, root.ScanStartUtcTicks, root.ScanEndUtcTicks,
            root.Size, root.FileEntryCount, root.DirEntryCount);

        var len = new long[ColumnCount];
        len[(int)Col.ModifiedTicks] = (long)count * sizeof(long);
        len[(int)Col.Size] = (long)count * sizeof(long);
        len[(int)Col.BitFields] = count;
        len[(int)Col.Parent] = (long)count * sizeof(int);
        len[(int)Col.FirstChild] = (long)count * sizeof(int);
        len[(int)Col.NextSibling] = (long)count * sizeof(int);
        len[(int)Col.NameOffsets] = (long)(count + 1) * sizeof(long);
        len[(int)Col.NameBlob] = nameBytes;
        len[(int)Col.Hash] = hasHashes ? (long)count * 16 : 0;
        len[(int)Col.Meta] = meta.Length;

        var off = new long[ColumnCount];
        var pos = (long)HeaderSize;
        for (var c = 0; c < ColumnCount; c++)
        {
            pos = Align8(pos);
            off[c] = pos;
            pos += len[c];
        }

        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), Version);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), count);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), ComposeFlags(hasHashes, root.FollowJunctions));
        for (var c = 0; c < ColumnCount; c++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(PreambleFixed + c * 16), off[c]);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(PreambleFixed + c * 16 + 8), len[c]);
        }

        // ---- pass 2: stream the columns ----
        using var handle = File.OpenHandle(outPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
        RandomAccess.SetLength(handle, pos); // gap bytes between columns stay zero, like the padded sequential write
        RandomAccess.Write(handle, header, 0);

        var modified = new ColumnBuffer(handle, off[(int)Col.ModifiedTicks]);
        var size = new ColumnBuffer(handle, off[(int)Col.Size]);
        var bits = new ColumnBuffer(handle, off[(int)Col.BitFields]);
        var parents = new ColumnBuffer(handle, off[(int)Col.Parent]);
        var firsts = new ColumnBuffer(handle, off[(int)Col.FirstChild]);
        var nexts = new ColumnBuffer(handle, off[(int)Col.NextSibling]);
        var nameOffsets = new ColumnBuffer(handle, off[(int)Col.NameOffsets]);
        var names = new ColumnBuffer(handle, off[(int)Col.NameBlob]);
        var hashes = hasHashes ? new ColumnBuffer(handle, off[(int)Col.Hash]) : null;

        // root (index 0)
        modified.Int64(root.ModifiedTicks);
        size.Int64(root.Size);
        bits.Byte((byte)root.BitFields);
        parents.Int32(NoEntry);
        firsts.Int32(firstChildOfDir[0]);
        nexts.Int32(NoEntry);
        nameOffsets.Int64(0);
        names.Utf8(root.Path);
        hashes?.Zeros(16);

        var nameBlobPos = (long)rootNameBytes;
        var next = 1;
        var dirOrdinal = 0;
        var walk = new Stack<(IList<DirEntry> Kids, int ParentIdx)>();
        walk.Push((root.Children, 0));
        while (walk.Count > 0)
        {
            var (kids, parentIdx) = walk.Pop();
            if (kids == null) continue;

            var n = kids.Count;
            for (var k = 0; k < n; k++)
            {
                var child = kids[k];
                var idx = next++;

                modified.Int64(child.ModifiedTicks);
                size.Int64(child.Size);
                bits.Byte((byte)child.BitFields);
                parents.Int32(parentIdx);
                nexts.Int32(k == n - 1 ? NoEntry : idx + 1);

                nameOffsets.Int64(nameBlobPos);
                names.Utf8(child.NamePart);
                names.Utf8(child.ExtPart);
                nameBlobPos += Utf8Length(child.NamePart) + Utf8Length(child.ExtPart);

                if (hashes != null)
                {
                    if (child.IsHashDone) hashes.Hash(child.Hash);
                    else hashes.Zeros(16);
                }

                if (child.IsDirectory)
                {
                    var ordinal = ++dirOrdinal;
                    firsts.Int32(firstChildOfDir[ordinal]);
                    walk.Push((child.Children, idx));
                }
                else
                {
                    firsts.Int32(NoEntry);
                }
            }
        }

        if (next != count || nameBlobPos != nameBytes)
        {
            throw new InvalidOperationException("The catalog tree changed while it was being written.");
        }

        nameOffsets.Int64(nameBlobPos);

        foreach (var col in new[] { modified, size, bits, parents, firsts, nexts, nameOffsets, names, hashes })
        {
            col?.Flush();
        }

        RandomAccess.Write(handle, meta, off[(int)Col.Meta]);
    }

    private static int Utf8Length(string value)
        => string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);

    /// <summary>A small write-behind buffer for one column, flushed to its own file offset.</summary>
    private sealed class ColumnBuffer
    {
        private const int Capacity = 64 * 1024;
        private readonly SafeFileHandle _handle;
        private byte[] _buffer = new byte[Capacity];
        private int _length;
        private long _filePosition;

        public ColumnBuffer(SafeFileHandle handle, long offset)
        {
            _handle = handle;
            _filePosition = offset;
        }

        private Span<byte> Reserve(int bytes)
        {
            if (_length + bytes > _buffer.Length)
            {
                Flush();
                if (bytes > _buffer.Length)
                {
                    _buffer = new byte[bytes];
                }
            }

            var span = _buffer.AsSpan(_length, bytes);
            _length += bytes;
            return span;
        }

        public void Int64(long value) => BinaryPrimitives.WriteInt64LittleEndian(Reserve(sizeof(long)), value);
        public void Int32(int value) => BinaryPrimitives.WriteInt32LittleEndian(Reserve(sizeof(int)), value);
        public void Byte(byte value) => Reserve(1)[0] = value;
        public void Zeros(int bytes) => Reserve(bytes).Clear();
        public void Hash(Hash16 hash) => MemoryMarshal.Write(Reserve(16), in hash);

        public void Utf8(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var bytes = Encoding.UTF8.GetByteCount(value);
            Encoding.UTF8.GetBytes(value, Reserve(bytes));
        }

        public void Flush()
        {
            if (_length == 0) return;
            RandomAccess.Write(_handle, _buffer.AsSpan(0, _length), _filePosition);
            _filePosition += _length;
            _length = 0;
        }
    }
}
