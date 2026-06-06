using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace cdeMemProbe.Columnar;

/// <summary>
/// SPIKE — zero-copy reader over a <see cref="ColumnarFormat"/> file. "Loading" is just mmap-ing:
/// columns are exposed as <see cref="ReadOnlySpan{T}"/> straight over the mapping, so no catalog
/// data is copied to the managed heap. A name search byte-scans the UTF-8 NameBlob in place
/// (zero managed allocation per entry), and only the pages it touches fault into the working set.
/// </summary>
public sealed unsafe class ColumnarReader : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _base;
    private readonly long _length;

    private readonly long[] _off = new long[ColumnarFormat.ColumnCount];
    private readonly long[] _len = new long[ColumnarFormat.ColumnCount];

    public int Count { get; }
    public bool HasHashes { get; }

    public ColumnarReader(string path)
    {
        var fileLen = new System.IO.FileInfo(path).Length;
        _length = fileLen;
        _mmf = MemoryMappedFile.CreateFromFile(path, System.IO.FileMode.Open, mapName: null,
            capacity: 0, MemoryMappedFileAccess.Read);
        _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _base);

        var header = new ReadOnlySpan<byte>(_base, ColumnarFormat.HeaderSize);
        if (!header[..4].SequenceEqual(ColumnarFormat.Magic))
            throw new InvalidDataException($"not a CDEX file: {path}");
        var version = BitConverter.ToInt32(header.Slice(4, 4));
        if (version != ColumnarFormat.Version)
            throw new InvalidDataException($"unsupported CDEX version {version}");
        Count = BitConverter.ToInt32(header.Slice(8, 4));
        HasHashes = (BitConverter.ToInt32(header.Slice(12, 4)) & ColumnarFormat.FlagHasHashes) != 0;

        var p = ColumnarFormat.PreambleFixed;
        for (var c = 0; c < ColumnarFormat.ColumnCount; c++)
        {
            _off[c] = BitConverter.ToInt64(header.Slice(p, 8)); p += 8;
            _len[c] = BitConverter.ToInt64(header.Slice(p, 8)); p += 8;
        }
    }

    private ReadOnlySpan<byte> Bytes(ColumnarFormat.Col col)
        => new(_base + _off[(int)col], (int)_len[(int)col]);

    private ReadOnlySpan<T> As<T>(ColumnarFormat.Col col) where T : struct
        => MemoryMarshal.Cast<byte, T>(Bytes(col));

    public ReadOnlySpan<int> Parent => As<int>(ColumnarFormat.Col.Parent);
    public ReadOnlySpan<long> Size => As<long>(ColumnarFormat.Col.Size);
    private ReadOnlySpan<int> NameOffsets => As<int>(ColumnarFormat.Col.NameOffsets);
    private ReadOnlySpan<byte> NameBlob => Bytes(ColumnarFormat.Col.NameBlob);

    /// <summary>UTF-8 full-name bytes of entry <paramref name="i"/>, sliced in place from the mapping.</summary>
    public ReadOnlySpan<byte> Name(int i)
    {
        var offs = NameOffsets;
        return NameBlob.Slice(offs[i], offs[i + 1] - offs[i]);
    }

    /// <summary>
    /// Zero-allocation name search: byte-scan each entry's UTF-8 name for <paramref name="patternUtf8"/>
    /// (ASCII case-insensitive). Sequentially touches only the NameBlob + NameOffsets columns.
    /// </summary>
    public int FindName(ReadOnlySpan<byte> patternUtf8, Action<int> onMatch = null)
    {
        var offs = NameOffsets;
        var blob = NameBlob;
        var matches = 0;
        for (var i = 0; i < Count; i++)
        {
            var name = blob.Slice(offs[i], offs[i + 1] - offs[i]);
            if (AsciiContainsIgnoreCase(name, patternUtf8))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        return matches;
    }

    /// <summary>
    /// Path search: build each entry's full path bytes into a reused buffer by walking Parent[],
    /// then byte-scan. Still no per-entry managed string allocation.
    /// </summary>
    public int FindPath(ReadOnlySpan<byte> patternUtf8, Action<int> onMatch = null)
    {
        var parent = Parent;
        var offs = NameOffsets;
        var blob = NameBlob;
        Span<int> chain = stackalloc int[256];
        var buf = new byte[1024];
        var matches = 0;

        for (var i = 0; i < Count; i++)
        {
            var depth = 0;
            for (var cur = i; cur != -1 && depth < chain.Length; cur = parent[cur]) chain[depth++] = cur;

            var n = 0;
            for (var k = depth - 1; k >= 0; k--)
            {
                if (n > 0) buf = Append(buf, ref n, (byte)'\\');
                var idx = chain[k];
                var name = blob.Slice(offs[idx], offs[idx + 1] - offs[idx]);
                buf = Append(buf, ref n, name);
            }

            if (AsciiContainsIgnoreCase(buf.AsSpan(0, n), patternUtf8))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        return matches;
    }

    public string FullPath(int i)
    {
        var parent = Parent;
        var offs = NameOffsets;
        var blob = NameBlob;
        Span<int> chain = stackalloc int[256];
        var depth = 0;
        for (var cur = i; cur != -1 && depth < chain.Length; cur = parent[cur]) chain[depth++] = cur;
        var sb = new StringBuilder(128);
        for (var k = depth - 1; k >= 0; k--)
        {
            if (sb.Length > 0) sb.Append('\\');
            var idx = chain[k];
            sb.Append(Encoding.UTF8.GetString(blob.Slice(offs[idx], offs[idx + 1] - offs[idx])));
        }
        return sb.ToString();
    }

    private static byte[] Append(byte[] buf, ref int n, byte b)
    {
        if (n + 1 > buf.Length) Array.Resize(ref buf, buf.Length * 2);
        buf[n++] = b;
        return buf;
    }

    private static byte[] Append(byte[] buf, ref int n, ReadOnlySpan<byte> src)
    {
        while (n + src.Length > buf.Length) Array.Resize(ref buf, buf.Length * 2);
        src.CopyTo(buf.AsSpan(n));
        n += src.Length;
        return buf;
    }

    private static bool AsciiContainsIgnoreCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty) return true;
        if (haystack.Length < needle.Length) return false;
        var last = haystack.Length - needle.Length;
        for (var i = 0; i <= last; i++)
        {
            var k = 0;
            for (; k < needle.Length; k++)
            {
                if (ToLower(haystack[i + k]) != ToLower(needle[k])) break;
            }
            if (k == needle.Length) return true;
        }
        return false;
    }

    private static byte ToLower(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b;

    public void Dispose()
    {
        if (_base != null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _base = null;
        }
        _view?.Dispose();
        _mmf?.Dispose();
    }
}
