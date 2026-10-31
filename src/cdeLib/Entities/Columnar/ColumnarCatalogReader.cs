using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;

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
// Deliberate: the columns are exposed as spans straight over the memory-mapped view (zero-copy design).
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6640:Using unsafe code blocks is security-sensitive",
    Justification = "Zero-copy reads over a read-only memory-mapped file; the pointer is only exposed as spans sized from the column offset/length table.")]
public sealed unsafe class ColumnarCatalogReader : IEntrySource, IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _base;

    private readonly long[] _off = new long[ColumnarFormat.ColumnCount];
    private readonly long[] _len = new long[ColumnarFormat.ColumnCount];

    /// <summary>The path this catalog was opened from.</summary>
    public string FilePath { get; }

    public int Count { get; }
    public bool HasHashes { get; }

    /// <summary>True if the catalog was scanned following directory junctions/reparse points (#66). False for older files.</summary>
    public bool FollowJunctions { get; }

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
        FilePath = path;
        _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName: null,
            capacity: 0, MemoryMappedFileAccess.Read);
        _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _base);

        var header = new ReadOnlySpan<byte>(_base, ColumnarFormat.HeaderSize);
        if (!header[..4].SequenceEqual(ColumnarFormat.Magic))
        {
            Dispose(); // don't leak the mapping (and the file lock) when the constructor throws
            throw new InvalidDataException($"not a CDEX catalog: {path}");
        }

        var version = BitConverter.ToInt32(header.Slice(4, 4));
        if (version != ColumnarFormat.Version)
        {
            Dispose();
            throw new InvalidDataException(
                $"unsupported CDEX version {version} (expected {ColumnarFormat.Version}): {path}");
        }

        Count = BitConverter.ToInt32(header.Slice(8, 4));
        var flags = BitConverter.ToInt32(header.Slice(12, 4));
        HasHashes = (flags & ColumnarFormat.FlagHasHashes) != 0;
        FollowJunctions = (flags & ColumnarFormat.FlagFollowJunctions) != 0;

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

    /// <summary>The raw bytes of one column, for writers that copy a catalog column by column.</summary>
    internal ReadOnlySpan<byte> ColumnBytes(ColumnarFormat.Col col) => Bytes(col);

    private ReadOnlySpan<T> As<T>(ColumnarFormat.Col col) where T : struct
        => MemoryMarshal.Cast<byte, T>(Bytes(col));

    public ReadOnlySpan<long> Size => As<long>(ColumnarFormat.Col.Size);
    public ReadOnlySpan<long> ModifiedTicks => As<long>(ColumnarFormat.Col.ModifiedTicks);
    public ReadOnlySpan<byte> BitFields => Bytes(ColumnarFormat.Col.BitFields);
    public ReadOnlySpan<int> Parent => As<int>(ColumnarFormat.Col.Parent);
    private ReadOnlySpan<int> FirstChild => As<int>(ColumnarFormat.Col.FirstChild);
    private ReadOnlySpan<int> NextSibling => As<int>(ColumnarFormat.Col.NextSibling);
    private ReadOnlySpan<byte> HashBytes => Bytes(ColumnarFormat.Col.Hash);
    private ReadOnlySpan<long> NameOffsets => As<long>(ColumnarFormat.Col.NameOffsets);
    private ReadOnlySpan<byte> NameBlob => Bytes(ColumnarFormat.Col.NameBlob);

    public Flags Flags(int i) => (Flags)BitFields[i];
    public bool IsDirectory(int i) => (Flags(i) & Entities.Flags.Directory) == Entities.Flags.Directory;

    // ----- IEntrySource: index-addressed accessors straight over the mapping -----
    public long SizeOf(int i) => Size[i];
    public DateTime ModifiedOf(int i) => DateTime.FromBinary(ModifiedTicks[i]);
    public long ModifiedTicksOf(int i) => ModifiedTicks[i];
    public Flags FlagsOf(int i) => Flags(i);
    public bool IsHashDone(int i) => Flags(i).IsHashUsable();
    public bool IsPartialHash(int i) => (Flags(i) & Entities.Flags.PartialHash) == Entities.Flags.PartialHash;
    public bool HasHash => HasHashes;
    public Hash16 HashOf(int i) =>
        HasHashes ? MemoryMarshal.Read<Hash16>(HashBytes.Slice(i * 16, 16)) : default;
    public string FullName(int i) => Name(i);
    public string NameOf(int i) => Name(i); // full name (not split) — fine for path-problem trailing checks
    public int ParentOf(int i) => Parent[i];
    public int FirstChildOf(int i) => FirstChild[i];
    public int NextSiblingOf(int i) => NextSibling[i];

    public IEnumerable<int> ChildrenOf(int parent)
    {
        // Materialise into a list (no yield): the sibling chain reads spans over the mapping, which a
        // lazy iterator's state machine can't hold. Per-directory child fan-out is small.
        var result = new List<int>();
        var first = FirstChild[parent];
        if (first == EntryStore.None) return result;
        var sib = NextSibling;
        for (var c = first; c != EntryStore.None; c = sib[c]) result.Add(c);
        return result;
    }

    public void AppendFullPath(StringBuilder sb, int i)
    {
        byte[] buf = null;
        int[] chain = null;
        var n = BuildPathBytes(ref buf, ref chain, i);
        if (sb.Length > 0)
        {
            var last = sb[^1];
            if (last != '\\' && last != '/') sb.Append(Path.DirectorySeparatorChar);
        }
        sb.Append(Encoding.UTF8.GetString(buf, 0, n));
    }

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

    /// <summary>
    /// Full-filter search (pattern + name/path + file/folder + size/date/hour ranges) matching the GUI
    /// <see cref="EntryStoreSearch.Find(EntryStore,EntryStoreFindOptions,Action{int},Func{bool},Action{int})"/>
    /// semantics exactly, evaluated zero-copy over the mapping.
    /// </summary>
    public void Find(EntryStoreFindOptions o, Action<int> onMatch,
        Func<bool> isCancelled = null, Action<int> onScan = null)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(onMatch);
        if (!o.IncludeFiles && !o.IncludeFolders) return;

        // An Everything-style query is parsed once here; it overrides regex mode.
        var everything = o.EverythingMode ? EverythingQuery.Parse(o.Pattern ?? string.Empty) : null;
        var regexMode = o.RegexMode && everything is null;
        var hasPattern = everything is null ? !string.IsNullOrEmpty(o.Pattern) : !everything.IsEmpty;
        var regex = regexMode && hasPattern
            ? new BoundedRegex(o.Pattern) // timeout per entry = no match; repeated timeouts end the scan
            : null;
        var matcher = hasPattern && !regexMode && everything is null ? new Utf8Matcher(o.Pattern) : default;

        var size = Size;
        var bits = BitFields;
        var offs = NameOffsets;
        var blob = NameBlob;
        var texts = everything?.TextsRead(o.IncludePath) ?? EverythingTexts.Name;
        var entryText = texts == EverythingTexts.Both ? new EntryText(this) : null;
        var needPathBuf = everything is null ? o.IncludePath : texts == EverythingTexts.Path;
        var pathBuf = needPathBuf ? new byte[1024] : null;
        var chain = needPathBuf ? new int[64] : null;
        var chars = regexMode ? new char[512] : null; // reused decode buffer for span regex matching

        for (var i = 1; i < Count; i++) // index 0 is the root, never a result
        {
            if ((i & 4095) == 0)
            {
                if (isCancelled != null && isCancelled()) return;
                onScan?.Invoke(i);
            }
            if (regex is { Exhausted: true }) break; // pattern timed out repeatedly; abandon the scan

            var isDir = ((Flags)bits[i] & Entities.Flags.Directory) == Entities.Flags.Directory;
            if (isDir ? !o.IncludeFolders : !o.IncludeFiles) continue;

            if (o.FromSizeEnable && size[i] < o.FromSize) continue;
            if (o.ToSizeEnable && size[i] > o.ToSize) continue;

            if (o.FromDateEnable || o.ToDateEnable || o.FromHourEnable || o.ToHourEnable || o.NotOlderThanEnable)
            {
                var modified = ModifiedOf(i);
                if (o.FromDateEnable && modified < o.FromDate) continue;
                if (o.ToDateEnable && modified > o.ToDate) continue;
                if (o.NotOlderThanEnable && modified < o.NotOlderThan) continue;
                var tod = modified.TimeOfDay;
                if (o.FromHourEnable && tod < o.FromHour) continue;
                if (o.ToHourEnable && tod > o.ToHour) continue;
            }

            if (!hasPattern) { onMatch(i); continue; }

            bool match;
            if (everything is not null)
            {
                switch (texts)
                {
                    case EverythingTexts.Name:
                        match = everything.Matches(blob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i])), isDir);
                        break;
                    case EverythingTexts.Path:
                        var n = BuildPathBytes(ref pathBuf, ref chain, i);
                        match = everything.Matches(pathBuf.AsSpan(0, n), isDir);
                        break;
                    default:
                        // Terms for both texts: the path is built only if a term reaches one that needs it.
                        entryText.Reset(i, (int)offs[i], (int)(offs[i + 1] - offs[i]), isDir);
                        match = everything.Matches(entryText, o.IncludePath);
                        break;
                }
            }
            else if (regex is not null)
            {
                ReadOnlySpan<byte> utf8;
                if (o.IncludePath)
                {
                    var n = BuildPathBytes(ref pathBuf, ref chain, i);
                    utf8 = pathBuf.AsSpan(0, n);
                }
                else
                {
                    utf8 = blob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i]));
                }
                match = regex.IsMatch(DecodeUtf8(utf8, ref chars));
            }
            else if (o.IncludePath)
            {
                var n = BuildPathBytes(ref pathBuf, ref chain, i);
                match = matcher.Contains(pathBuf.AsSpan(0, n));
            }
            else
            {
                match = matcher.Contains(blob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i])));
            }

            if (match) onMatch(i);
        }
        regex?.ReportIfExhausted(); // never truncate silently
    }

    // Decode UTF-8 into a reused char buffer (grown as needed) and return the decoded span, so regex
    // matching runs over a span with no per-entry string. Invalid sequences decode exactly as
    // Encoding.UTF8.GetString would (U+FFFD), so results match the string-based path.
    private static ReadOnlySpan<char> DecodeUtf8(ReadOnlySpan<byte> utf8, ref char[] chars)
    {
        // UTF-8 decoding never yields more chars than bytes.
        if (utf8.Length > chars.Length) chars = new char[Math.Max(utf8.Length, chars.Length * 2)];
        var n = Encoding.UTF8.GetChars(utf8, chars);
        return chars.AsSpan(0, n);
    }

    // Build entry i's full-path UTF-8 bytes into buf (grown as needed); returns the byte length.
    // buf/chain may be null on entry (allocated on first use) and are reused across calls by the
    // scan loops, so steady state allocates nothing.
    // One entry's name and (built only when asked for) full path, for an Everything query. One instance serves a
    // whole Find; Reset moves it to the next entry.
    private sealed class EntryText(ColumnarCatalogReader reader) : IEntryText
    {
        private byte[] _pathBuf;
        private int[] _chain;
        private int _index;
        private int _nameStart;
        private int _nameLength;
        private int _pathLength = -1;

        public bool IsFolder { get; private set; }

        public void Reset(int index, int nameStart, int nameLength, bool isFolder)
        {
            IsFolder = isFolder;
            _index = index;
            _nameStart = nameStart;
            _nameLength = nameLength;
            _pathLength = -1;
        }

        public ReadOnlySpan<byte> Name => reader.NameBlob.Slice(_nameStart, _nameLength);

        public ReadOnlySpan<byte> Path
        {
            get
            {
                if (_pathLength < 0) _pathLength = reader.BuildPathBytes(ref _pathBuf, ref _chain, _index);
                return _pathBuf.AsSpan(0, _pathLength);
            }
        }
    }

    private int BuildPathBytes(ref byte[] buf, ref int[] chain, int i)
    {
        var depth = CollectChain(i, ref chain);
        var len = WritePath(chain.AsSpan(0, depth), default, write: false);
        if (buf == null || len > buf.Length) buf = new byte[Math.Max(len, Math.Max(1024, (buf?.Length ?? 0) * 2))];
        WritePath(chain.AsSpan(0, depth), buf, write: true);
        return len;
    }

    // Walk the Parent column from i to the root, storing the indices root-LAST in chain[0..depth)
    // (leaf first); returns depth. Grows chain if needed.
    private int CollectChain(int i, ref int[] chain)
    {
        var parent = Parent;
        var depth = 0;
        for (var cur = i; cur != EntryStore.None && depth < Count; cur = parent[cur]) depth++; // bounded: a corrupt cycle truncates

        if (chain == null || depth > chain.Length) chain = new int[Math.Max(depth, Math.Max(64, (chain?.Length ?? 0) * 2))];
        var k = 0;
        for (var cur = i; cur != EntryStore.None && k < depth; cur = parent[cur]) chain[k++] = cur;
        return depth;
    }

    // Lay out the path root-first from a leaf-first chain. With write:false only measures. Returns the
    // total byte length. A separator goes between segments, but not after one already ending in one.
    private int WritePath(ReadOnlySpan<int> chain, Span<byte> dest, bool write)
    {
        var offs = NameOffsets;
        var blob = NameBlob;
        var n = 0;
        byte last = 0;
        for (var k = chain.Length - 1; k >= 0; k--)
        {
            if (n > 0 && last != '\\' && last != '/')
            {
                if (write) dest[n] = (byte)Path.DirectorySeparatorChar;
                n++;
                last = (byte)Path.DirectorySeparatorChar;
            }
            var idx = chain[k];
            var seg = blob.Slice((int)offs[idx], (int)(offs[idx + 1] - offs[idx]));
            if (seg.Length == 0) continue;
            if (write) seg.CopyTo(dest[n..]);
            n += seg.Length;
            last = seg[^1];
        }
        return n;
    }

    private int FindRegex(string pattern, bool includePath, bool includeFiles, bool includeFolders,
        Action<int> onMatch)
    {
        var regex = new BoundedRegex(pattern);
        var bits = BitFields;
        var offs = NameOffsets;
        var blob = NameBlob;
        byte[] pathBuf = null;
        int[] chain = null;
        var chars = new char[512];
        var matches = 0;
        for (var i = 1; i < Count; i++)
        {
            if (regex.Exhausted) break; // pattern timed out repeatedly; abandon the scan
            if (!Wanted(bits[i], includeFiles, includeFolders)) continue;
            ReadOnlySpan<byte> utf8;
            if (includePath)
            {
                var n = BuildPathBytes(ref pathBuf, ref chain, i);
                utf8 = pathBuf.AsSpan(0, n);
            }
            else
            {
                utf8 = blob.Slice((int)offs[i], (int)(offs[i + 1] - offs[i]));
            }
            if (regex.IsMatch(DecodeUtf8(utf8, ref chars)))
            {
                matches++;
                onMatch?.Invoke(i);
            }
        }
        regex.ReportIfExhausted(); // never truncate silently
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
        var bits = BitFields;
        int[] chain = null;
        byte[] buf = null;
        var matches = 0;

        for (var i = 1; i < Count; i++) // index 0 is the root, never a result
        {
            if (!Wanted(bits[i], includeFiles, includeFolders)) continue;

            var n = BuildPathBytes(ref buf, ref chain, i);

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

    /// <summary>
    /// Full path of entry <paramref name="i"/> with exactly one managed allocation (the result string):
    /// segment bytes are laid out into a stack buffer (pooled when long) and decoded once.
    /// </summary>
    public string FullPath(int i)
    {
        int[] chainArr = null;
        byte[] bytesArr = null;
        try
        {
            Span<int> chain = stackalloc int[64];
            var parent = Parent;
            var depth = 0;
            for (var cur = i; cur != EntryStore.None && depth < Count; cur = parent[cur]) depth++; // bounded: a corrupt cycle truncates

            if (depth > chain.Length)
            {
                chainArr = ArrayPool<int>.Shared.Rent(depth);
                chain = chainArr;
            }
            chain = chain[..depth];
            var k = 0;
            for (var cur = i; cur != EntryStore.None && k < depth; cur = parent[cur]) chain[k++] = cur;

            var len = WritePath(chain, default, write: false);
            Span<byte> bytes = stackalloc byte[512];
            if (len > bytes.Length)
            {
                bytesArr = ArrayPool<byte>.Shared.Rent(len);
                bytes = bytesArr;
            }
            WritePath(chain, bytes, write: true);
            return Encoding.UTF8.GetString(bytes[..len]);
        }
        finally
        {
            if (chainArr != null) ArrayPool<int>.Shared.Return(chainArr);
            if (bytesArr != null) ArrayPool<byte>.Shared.Return(bytesArr);
        }
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
