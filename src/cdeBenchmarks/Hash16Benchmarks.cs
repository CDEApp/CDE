using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using cdeLib.Entities;

namespace cdeBenchmarks;

/// <summary>
/// Benchmarks for Hash16 operations to measure the performance impact of struct vs class implementation.
/// Hash16 is used for millions of file entries, so any optimization here has massive impact.
///
/// Key scenarios benchmarked:
/// 1. Creation from byte arrays (MD5 hashes)
/// 2. Equality comparisons (used heavily in duplicate detection)
/// 3. Dictionary lookups (hash-based grouping)
/// 4. Memory allocation patterns
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob]  // Let BenchmarkDotNet auto-detect runtime (.NET 10)
public class Hash16Benchmarks
{
    private byte[] _hash1 = null!;
    private byte[] _hash2 = null!;
    private byte[] _hash3 = null!;
    private Hash16 _structHash1;
    private Hash16 _structHash2;
    private Hash16 _structHash3;
    private Dictionary<Hash16, List<string>> _hashDictionary = null!;

    [Params(1000, 10000, 100000)]
    public int IterationCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Simulate real MD5 hashes (16 bytes each)
        _hash1 = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        _hash2 = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        _hash3 = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 };

        _structHash1 = new Hash16(_hash1);
        _structHash2 = new Hash16(_hash2);
        _structHash3 = new Hash16(_hash3);

        // Pre-populate dictionary for lookup benchmarks
        _hashDictionary = new Dictionary<Hash16, List<string>>(IterationCount);
        for (int i = 0; i < IterationCount; i++)
        {
            var hash = new Hash16(CreateVariedHash(i));
            _hashDictionary[hash] = new List<string> { $"file_{i}.txt" };
        }
    }

    /// <summary>
    /// Benchmark: Creating Hash16 instances from byte arrays.
    /// This simulates the cost during catalog loading when hashes are deserialized.
    ///
    /// Expected: Struct should be faster (stack allocation) and use less memory.
    /// </summary>
    [Benchmark(Description = "Create Hash16 from byte arrays")]
    public Hash16 CreateFromByteArray()
    {
        Hash16 result = default;
        for (int i = 0; i < IterationCount; i++)
        {
            result = new Hash16(_hash1);
        }
        return result;
    }

    /// <summary>
    /// Benchmark: Equality comparison between Hash16 instances.
    /// This is critical for duplicate file detection where millions of hashes are compared.
    ///
    /// Expected: Struct equality should be as fast or faster (inlined value comparison).
    /// </summary>
    [Benchmark(Description = "Hash16 equality comparisons")]
    public bool EqualityComparison()
    {
        bool result = false;
        for (int i = 0; i < IterationCount; i++)
        {
            result = _structHash1 == _structHash2; // Same hash
            result = _structHash1 == _structHash3; // Different hash
        }
        return result;
    }

    /// <summary>
    /// Benchmark: Using Hash16 as dictionary keys.
    /// This simulates duplicate detection where files are grouped by hash.
    ///
    /// Expected: Struct should have similar or better performance with GetHashCode inlined.
    /// </summary>
    [Benchmark(Description = "Dictionary lookups with Hash16 keys")]
    public int DictionaryLookup()
    {
        int count = 0;
        var lookupHash = new Hash16(CreateVariedHash(IterationCount / 2));

        for (int i = 0; i < 100; i++)
        {
            if (_hashDictionary.TryGetValue(lookupHash, out var files))
            {
                count += files.Count;
            }
        }
        return count;
    }

    /// <summary>
    /// Benchmark: Bulk dictionary insertion with Hash16 keys.
    /// This simulates building the duplicate detection hash map.
    ///
    /// Expected: Struct should use less memory and have similar throughput.
    /// Memory diagnostic will show the allocation difference clearly.
    /// </summary>
    [Benchmark(Description = "Dictionary insertion with Hash16 keys")]
    public Dictionary<Hash16, List<string>> DictionaryInsertion()
    {
        var dict = new Dictionary<Hash16, List<string>>(IterationCount);

        for (int i = 0; i < IterationCount; i++)
        {
            var hash = new Hash16(CreateVariedHash(i));
            if (!dict.TryGetValue(hash, out var list))
            {
                list = new List<string>();
                dict[hash] = list;
            }
            list.Add($"file_{i}.txt");
        }

        return dict;
    }

    /// <summary>
    /// Benchmark: Checking if Hash16 is set (non-zero).
    /// Used frequently during catalog operations to determine if files have been hashed.
    ///
    /// Expected: Struct property access should be inlined and very fast.
    /// </summary>
    [Benchmark(Description = "IsSet property check")]
    public bool CheckIsSet()
    {
        bool result = false;
        for (int i = 0; i < IterationCount; i++)
        {
            // ReSharper disable once RedundantAssignment
            result = _structHash1.IsSet;
            result = Hash16.Empty.IsSet;
        }
        return result;
    }

    /// <summary>
    /// Helper to create varied hashes for realistic benchmarking
    /// </summary>
    private static byte[] CreateVariedHash(int seed)
    {
        var hash = new byte[16];
        var seedBytes = BitConverter.GetBytes(seed);
        Buffer.BlockCopy(seedBytes, 0, hash, 0, Math.Min(4, seedBytes.Length));

        // Fill rest with pseudo-random data based on seed
        for (int i = 4; i < 16; i++)
        {
            hash[i] = (byte)((seed * 31 + i) % 256);
        }

        return hash;
    }
}
