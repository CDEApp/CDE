using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using cdeLib.Entities.Soa;

namespace cdeLib.Entities.Columnar;

/// <summary>
/// Zero-copy reader over a <see cref="ColumnarFormat"/> catalog file. "Loading" is just mmap-ing:
/// columns are exposed as <see cref="ReadOnlySpan{T}"/> straight over the mapping, so no catalog data
/// is copied to the managed heap. A name search byte-scans the UTF-8 NameBlob in place (zero managed
/// allocation per entry on the common ASCII path), and only the pages it touches fault into the
/// working set.
///
/// The mapping stays open for the lifetime of the reader; hold it for as long as the catalog is
/// "loaded" and <see cref="Dispose"/> it on reload/exit. The file is read-only.
/// </summary>
public sealed unsafe class ColumnarCatalogReader : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _base;

    private readonly long[] _off = new long[ColumnarFormat.ColumnCount];
    private readonly long[] _len = new long[ColumnarFormat.ColumnCount];

    public int Count { get; }
    public bool HasHashes { get; }

    // Catalog-level metadata (parsed once from the small Meta blob — this is the only managed copy).
    public string RootPath { get; }
    public string VolumeName { get; }
    public string DefaultFileName { get; }
    public string ActualFileName { get; }
    public string DriveLetterHint { get; }
    public string Description { get; }
    public long AvailSpace { get; }
    public long TotalSpace { get; }
    public long ScanStartUtcTicks { get; }
    public long ScanEndUtcTicks { get; }
    public long RootSize { get; }
    public uint RootFileEntryCount { get; }
    public uint RootDirEntryCount { get; }

    public ColumnarCatalogReader(string path)
    {
        _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName: null,
            capacity: 0, MemoryMappedFileAccess.Read);
        _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _base);

        var header = new ReadOnlySpan<byte>(_base, ColumnarFormat.HeaderSize);
        if (!header[..4].SequenceEqual(ColumnarFormat.Magic))
            throw new InvalidDataException($"not a CDEX catalog: {path}");
        var version = BitConverter.ToInt32(header.Slice(4, 4));
        if (version != ColumnarFormat.Version)
            throw new InvalidDataException(
                $"unsupported CDEX version {version} (expected {ColumnarFormat.Version}): {path}");
        Count = BitConverter.ToInt32(header.Slice(8, 4));
        HasHashes = (BitConverter.ToInt32(header.Slice(12, 4)) & ColumnarFormat.FlagHasHashes) != 0;

        var p = ColumnarFormat.PreambleFixed;
        for (var c = 0; c < ColumnarFormat.ColumnCount; c++)
        {
            _off[c] = BitConverter.ToInt64(header.Slice(p, 8)); p += 8;
            _len[c] = BitConverter.ToInt64(header.Slice(p, 8)); p += 8;
        }

        // Parse the metadata blob (small, read once).
        var meta = Bytes(ColumnarFormat.Col.Meta);
        var mp = 0;
        RootPath = ReadLenString(meta, ref mp);
        VolumeName = ReadLenString(meta, ref mp);
        DefaultFileName = ReadLenString(meta, ref mp);
        ActualFileName = ReadLenString(meta, ref mp);
        DriveLetterHint = ReadLenString(meta, ref mp);
        Description = ReadLenString(meta, ref mp);
        AvailSpace = ReadI64(meta, ref mp);
        TotalSpace = ReadI64(meta, ref mp);
        ScanStartUtcTicks = ReadI64(meta, ref mp);
        ScanEndUtcTicks = ReadI64(meta, ref mp);
        RootSize = ReadI64(meta, ref mp);
        RootFileEntryCount = (uint)ReadI32(meta, ref mp);
        RootDirEntryCount = (uint)ReadI32(meta, ref mp);
    }

    private ReadOnlySpan<byte> Bytes(ColumnarFormat.Col col)
        => new(_base + _off[(int)col], (int)_len[(int)col]);

    private ReadOnlySpan<T> As<T>(ColumnarFormat.Col col) where T : struct
        => MemoryMarshal.Cast<byte, T>(Bytes(col));

    public ReadOnlySpan<long> Size => As<long>(ColumnarFormat.Col.Size);
    public ReadOnlySpan<long> ModifiedTicks => As<long>(ColumnarFormat.Col.ModifiedTicks);
    public ReadOnlySpan<byte> BitFields => Bytes(ColumnarFormat.Col.BitFields);
    public ReadOnlySpan<int> Parent => As<int>(ColumnarFormat.Col.Parent);
    private ReadOnlySpan<long> NameOffsets => As<long>(ColumnarFormat.Col.NameOffsets);
    private ReadOnlySpan<byte> NameBlob => Bytes(ColumnarFormat.Col.NameBlob);

    public Flags Flags(int i) => (Flags)BitFields[i];
    public bool IsDirectory(int i) => (Flags(i) & Entities.Flags.Directory) == Entities.Flags.Directory;

    /// <summary>UTF-8 full-name bytes of entry <paramref name="i"/>, sliced in place from the mapping.</summary>
    public ReadOnlySpan<byte> NameUtf8(int i)
    {
        var offs = NameOffsets;
        return NameBlob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i]));
    }

    public string Name(int i) => Encoding.UTF8.GetString(NameUtf8(i));

    /// <summary>
    /// Find matching the production <see cref="EntryStoreSearch"/> semantics exactly: pattern +
    /// name/path + file/folder filter, index 0 (root) never a result. Substring matching byte-scans
    /// the mapping (zero-alloc ASCII path); regex decodes per entry like the store search does.
    /// </summary>
    public int Find(string pattern, bool regexMode, bool includePath, bool includeFiles,
        bool includeFolders, Action<int> onMatch = null)
    {
        if (!includeFiles && !includeFolders) return 0;
        if (regexMode && !string.IsNullOrEmpty(pattern))
            return FindRegex(pattern, includePath, includeFiles, includeFolders, onMatch);
        return includePath
            ? FindPath(pattern, includeFiles, includeFolders, onMatch)
            : FindName(pattern, includeFiles, includeFolders, onMatch);
    }

    private int FindRegex(string pattern, bool includePath, bool includeFiles, bool includeFolders,
        Action<int> onMatch)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        var bits = BitFields;
        var matches = 0;
        for (var i = 1; i < Count; i++)
        {
            if (!Wanted(bits[i], includeFiles, includeFolders)) continue;
            var text = includePath ? FullPath(i) : Name(i);
            if (regex.IsMatch(text))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        return matches;
    }

    /// <summary>
    /// Name search: byte-scan each entry's UTF-8 name for <paramref name="pattern"/> (ordinal,
    /// case-insensitive). Sequentially touches only the NameBlob + NameOffsets columns. Zero managed
    /// allocation per entry for ASCII names; non-ASCII names fall back to a decoded comparison.
    /// </summary>
    public int FindName(string pattern, bool includeFiles, bool includeFolders, Action<int> onMatch = null)
    {
        var matcher = new Utf8Matcher(pattern);
        var offs = NameOffsets;
        var blob = NameBlob;
        var bits = BitFields;
        var matches = 0;
        for (var i = 1; i < Count; i++) // index 0 is the root, never a result
        {
            if (!Wanted(bits[i], includeFiles, includeFolders)) continue;
            var name = blob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i]));
            if (matcher.Contains(name))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        return matches;
    }

    /// <summary>
    /// Path search: build each entry's full path bytes into a reused buffer by walking Parent[], then
    /// match. No per-entry managed string allocation on the ASCII path.
    /// </summary>
    public int FindPath(string pattern, bool includeFiles, bool includeFolders, Action<int> onMatch = null)
    {
        var matcher = new Utf8Matcher(pattern);
        var parent = Parent;
        var offs = NameOffsets;
        var blob = NameBlob;
        var bits = BitFields;
        Span<int> chain = stackalloc int[256];
        var buf = new byte[1024];
        var matches = 0;

        for (var i = 1; i < Count; i++) // index 0 is the root, never a result
        {
            if (!Wanted(bits[i], includeFiles, includeFolders)) continue;

            var depth = 0;
            for (var cur = i; cur != EntryStore.None && depth < chain.Length; cur = parent[cur])
                chain[depth++] = cur;

            var n = 0;
            for (var k = depth - 1; k >= 0; k--)
            {
                if (n > 0) buf = Append(buf, ref n, (byte)Path.DirectorySeparatorChar);
                var idx = chain[k];
                buf = Append(buf, ref n, blob.Slice((int)offs[idx], (int)(offs[idx + 1] - offs[idx])));
            }

            if (matcher.Contains(buf.AsSpan(0, n)))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        return matches;
    }

    private static bool Wanted(byte bitField, bool includeFiles, bool includeFolders)
    {
        if (includeFiles && includeFolders) return true;
        var isDir = ((Flags)bitField & Entities.Flags.Directory) == Entities.Flags.Directory;
        return isDir ? includeFolders : includeFiles;
    }

    public string FullPath(int i)
    {
        var parent = Parent;
        var offs = NameOffsets;
        var blob = NameBlob;
        Span<int> chain = stackalloc int[256];
        var depth = 0;
        for (var cur = i; cur != EntryStore.None && depth < chain.Length; cur = parent[cur])
            chain[depth++] = cur;
        var sb = new StringBuilder(128);
        for (var k = depth - 1; k >= 0; k--)
        {
            if (sb.Length > 0) sb.Append(Path.DirectorySeparatorChar);
            var idx = chain[k];
            sb.Append(Encoding.UTF8.GetString(blob.Slice((int)offs[idx], (int)(offs[idx + 1] - offs[idx]))));
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

    private static string ReadLenString(ReadOnlySpan<byte> s, ref int p)
    {
        var len = BitConverter.ToInt32(s.Slice(p, 4)); p += 4;
        if (len == 0) return string.Empty;
        var str = Encoding.UTF8.GetString(s.Slice(p, len));
        p += len;
        return str;
    }

    private static long ReadI64(ReadOnlySpan<byte> s, ref int p) { var v = BitConverter.ToInt64(s.Slice(p, 8)); p += 8; return v; }
    private static int ReadI32(ReadOnlySpan<byte> s, ref int p) { var v = BitConverter.ToInt32(s.Slice(p, 4)); p += 4; return v; }

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
