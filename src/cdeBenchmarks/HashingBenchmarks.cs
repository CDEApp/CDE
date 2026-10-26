using BenchmarkDotNet.Attributes;
using cdeLib.Infrastructure.Hashing;

namespace cdeBenchmarks;

/// <summary>
/// Full-file MurmurHash3 over a 64 MB file: the original 4-bytes-at-a-time <c>BinaryReader.ReadBytes(4)</c>
/// loop versus the chunked pooled-buffer implementation. Look at the Allocated column (issue #35).
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class HashingBenchmarks
{
    private const uint Seed = 123456U;
    private string _path = null!;

    [GlobalSetup(Targets = [nameof(Legacy_ReadBytes4), nameof(Chunked_PooledBuffer)])]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"cde-hash-bench-{Guid.NewGuid():N}.bin");
        var data = new byte[64 * 1024 * 1024];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(_path, data);
    }

    [GlobalCleanup(Targets = [nameof(Legacy_ReadBytes4), nameof(Chunked_PooledBuffer)])]
    public void Cleanup()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Benchmark(Baseline = true)]
    public uint Legacy_ReadBytes4() => LegacyHash(File.OpenRead(_path), Seed);

    [Benchmark]
    public uint Chunked_PooledBuffer() => MurMurHash3.Hash(File.OpenRead(_path), Seed);

    // Issue #36: per-file overhead. Hashes many small files; Allocated/op should be near zero
    // apart from the file handle.
    private string _smallDir = null!;
    private string[] _smallFiles = null!;
    private HashHelper _helper = null!;

    [GlobalSetup(Targets = [nameof(PerFile_Partial_SmallFiles), nameof(PerFile_Full_SmallFiles)])]
    public void SetupSmallFiles()
    {
        _smallDir = Path.Combine(Path.GetTempPath(), $"cde-hash-small-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_smallDir);
        _smallFiles = new string[1000];
        var rnd = new Random(1);
        for (var i = 0; i < _smallFiles.Length; i++)
        {
            _smallFiles[i] = Path.Combine(_smallDir, $"f{i}.bin");
            var data = new byte[rnd.Next(1, 4096)];
            rnd.NextBytes(data);
            File.WriteAllBytes(_smallFiles[i], data);
        }

        _helper = new HashHelper(null!);
    }

    [GlobalCleanup(Targets = [nameof(PerFile_Partial_SmallFiles), nameof(PerFile_Full_SmallFiles)])]
    public void CleanupSmallFiles()
    {
        if (Directory.Exists(_smallDir)) Directory.Delete(_smallDir, true);
    }

    [Benchmark]
    public async Task<ulong> PerFile_Partial_SmallFiles()
    {
        ulong acc = 0;
        foreach (var f in _smallFiles) acc ^= (await _helper.GetHashResponseFromFile(f, 65536)).Hash;
        return acc;
    }

    [Benchmark]
    public async Task<ulong> PerFile_Full_SmallFiles()
    {
        ulong acc = 0;
        foreach (var f in _smallFiles) acc ^= (await _helper.GetHashResponseFromFile(f, null)).Hash;
        return acc;
    }

    // Verbatim copy of the original implementation, kept as the baseline.
    private static uint LegacyHash(Stream stream, uint seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        uint h1 = seed;
        uint streamLength = 0;

        using (var reader = new BinaryReader(stream))
        {
            var chunk = reader.ReadBytes(4);
            while (chunk.Length > 0)
            {
                streamLength += (uint)chunk.Length;
                uint k1;
                switch (chunk.Length)
                {
                    case 4:
                        k1 = (uint)(chunk[0] | chunk[1] << 8 | chunk[2] << 16 | chunk[3] << 24);
                        k1 *= c1;
                        k1 = Rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        h1 = Rotl32(h1, 13);
                        h1 = h1 * 5 + 0xe6546b64;
                        break;
                    case 3:
                        k1 = (uint)(chunk[0] | chunk[1] << 8 | chunk[2] << 16);
                        k1 *= c1;
                        k1 = Rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                    case 2:
                        k1 = (uint)(chunk[0] | chunk[1] << 8);
                        k1 *= c1;
                        k1 = Rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                    case 1:
                        k1 = chunk[0];
                        k1 *= c1;
                        k1 = Rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                }

                chunk = reader.ReadBytes(4);
            }
        }

        h1 ^= streamLength;
        h1 ^= h1 >> 16;
        h1 *= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }

    private static uint Rotl32(uint x, byte r) => (x << r) | (x >> (32 - r));
}
