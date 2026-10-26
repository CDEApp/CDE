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

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"cde-hash-bench-{Guid.NewGuid():N}.bin");
        var data = new byte[64 * 1024 * 1024];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(_path, data);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Benchmark(Baseline = true)]
    public uint Legacy_ReadBytes4() => LegacyHash(File.OpenRead(_path), Seed);

    [Benchmark]
    public uint Chunked_PooledBuffer() => MurMurHash3.Hash(File.OpenRead(_path), Seed);

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
