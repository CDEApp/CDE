using System.Buffers;
using System.IO.Hashing;
using BenchmarkDotNet.Attributes;
using cdeLib.Entities;
using cdeLib.Infrastructure.Hashing;
using Microsoft.Win32.SafeHandles;

namespace cdeBenchmarks;

[MemoryDiagnoser]
public class XxHash128InMemoryBenchmarks
{
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

    [Benchmark]
    public UInt128 XxHash128_Incremental()
    {
        var hasher = new XxHash128();
        ReadOnlySpan<byte> span = _data;
        for (var i = 0; i < span.Length; i += HashHelper.FullHashChunkSize)
            hasher.Append(span.Slice(i, Math.Min(HashHelper.FullHashChunkSize, span.Length - i)));
        return hasher.GetCurrentHashAsUInt128();
    }

    [Benchmark(Baseline = true)]
    public UInt128 XxHash128_OneShot() => XxHash128.HashToUInt128(_data);
}

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
    public async Task<Hash16> HashHelper_FullFile() => (await _helper.GetHashResponseFromFile(_path, null)).Hash;

    [Benchmark]
    public async Task<UInt128> XxHash128_RandomAccess()
    {
        using SafeFileHandle handle = File.OpenHandle(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hasher = new XxHash128();
        var buffer = ArrayPool<byte>.Shared.Rent(HashHelper.FullHashChunkSize);
        try
        {
            long position = 0;
            int read;
            while ((read = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, HashHelper.FullHashChunkSize), position)) > 0)
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
