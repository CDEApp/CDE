using System.Buffers;
using System.IO.Hashing;
using BenchmarkDotNet.Attributes;
using cdeLib.Infrastructure.Hashing;
using Microsoft.Win32.SafeHandles;

namespace cdeBenchmarks;

/// <summary>
/// Current MurmurHash3 x86_32 (chunked, pooled) versus XxHash128, CPU only (data already in memory).
/// </summary>
[MemoryDiagnoser]
public class XxHash128InMemoryBenchmarks
{
    private readonly MurmurHashWrapper _murmur = new();
    private byte[] _data = null!;

    // 64 KB = partial hash first pass (appsettings.json); 1 MB = full-hash chunk; 64 MB = large file.
    [Params(64 * 1024, 1 << 20, 64 << 20)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Size];
        new Random(42).NextBytes(_data); // NOSONAR S2245: benchmark test data, not security
    }

    /// <summary>Partial-hash path: MurmurHash.Net Hash32 over the buffer.</summary>
    [Benchmark(Baseline = true)]
    public ulong Murmur3_32_Library() => _murmur.Hash((ReadOnlySpan<byte>)_data);

    /// <summary>Full-hash path: the in-repo incremental state, fed in 1 MB chunks like HashWholeFile.</summary>
    [Benchmark]
    public uint Murmur3_32_Incremental()
    {
        var state = new MurMurHash3.Incremental(123456U);
        ReadOnlySpan<byte> span = _data;
        for (var i = 0; i < span.Length; i += MurMurHash3.StreamChunkSize)
            state.Append(span.Slice(i, Math.Min(MurMurHash3.StreamChunkSize, span.Length - i)));
        return state.Finish();
    }

    [Benchmark]
    public UInt128 XxHash128_Incremental()
    {
        var hasher = new XxHash128();
        ReadOnlySpan<byte> span = _data;
        for (var i = 0; i < span.Length; i += MurMurHash3.StreamChunkSize)
            hasher.Append(span.Slice(i, Math.Min(MurMurHash3.StreamChunkSize, span.Length - i)));
        return hasher.GetCurrentHashAsUInt128();
    }

    [Benchmark]
    public UInt128 XxHash128_OneShot() => XxHash128.HashToUInt128(_data);
}

/// <summary>
/// End-to-end full-file hash. The baseline is the real app path (HashHelper with bytesToHash null).
/// The XxHash128 variant uses the same RandomAccess + 1 MB pooled buffer loop as HashHelper.HashWholeFile,
/// so only the hash function differs. The file is in the OS cache after the first iteration, so this measures
/// syscall plus hash cost, not cold-disk speed.
/// </summary>
[MemoryDiagnoser]
public class XxHash128FileBenchmarks
{
    private const int FileSize = 256 << 20; // 256 MB
    private string _path = null!;
    private HashHelper _helper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"cde-xxh-bench-{Guid.NewGuid():N}.bin");
        var buf = new byte[1 << 20];
        var rnd = new Random(42); // NOSONAR S2245: benchmark test data, not security
        using (var fs = File.Create(_path))
        {
            for (var written = 0; written < FileSize; written += buf.Length)
            {
                rnd.NextBytes(buf);
                fs.Write(buf);
            }
        }

        _helper = new HashHelper(null!);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Benchmark(Baseline = true)]
    public async Task<ulong> HashHelper_FullFile() => (await _helper.GetHashResponseFromFile(_path, null)).Hash;

    [Benchmark]
    public async Task<UInt128> XxHash128_RandomAccess()
    {
        using SafeFileHandle handle = File.OpenHandle(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hasher = new XxHash128();
        var buffer = ArrayPool<byte>.Shared.Rent(MurMurHash3.StreamChunkSize);
        try
        {
            long position = 0;
            int read;
            while ((read = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, MurMurHash3.StreamChunkSize), position)) > 0)
            {
                hasher.Append(buffer.AsSpan(0, read));
                position += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return hasher.GetCurrentHashAsUInt128();
    }
}
