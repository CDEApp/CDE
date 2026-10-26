using System;
using System.IO;
using cdeLib.Infrastructure.Hashing;
using NUnit.Framework;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #35: the chunked streaming MurmurHash3 must be bit-identical to the original
/// 4-bytes-at-a-time implementation, because full hashes are persisted in existing catalogs.
/// </summary>
public class MurMurHash3StreamTests
{
    private const uint Seed = 123456U;

    private static byte[] RandomBytes(int length, int randomSeed)
    {
        var data = new byte[length];
        new Random(randomSeed).NextBytes(data);
        return data;
    }

    private static void AssertSameAsLegacy(byte[] data)
    {
        var expected = LegacyMurMurHash3.Hash(new MemoryStream(data), Seed);
        var actual = MurMurHash3.Hash(new MemoryStream(data), Seed);
        Assert.That(actual, Is.EqualTo(expected), $"length {data.Length}");
    }

    [Test]
    public void Matches_legacy_for_lengths_0_to_9([Range(0, 9)] int length)
        => AssertSameAsLegacy(RandomBytes(length, 100 + length));

    [Test]
    public void Matches_legacy_around_chunk_boundary([Range(-3, 3)] int delta)
    {
        var chunk = MurMurHash3.StreamChunkSize;
        AssertSameAsLegacy(RandomBytes(chunk + delta, 200 + delta));
        AssertSameAsLegacy(RandomBytes(2 * chunk + delta, 300 + delta));
    }

    [TestCase(1_000_003, 1)]
    [TestCase(3_145_729, 2)]
    [TestCase(5_000_001, 3)]
    [TestCase(7_340_033, 4)]
    public void Matches_legacy_for_random_large_sizes(int length, int randomSeed)
        => AssertSameAsLegacy(RandomBytes(length, randomSeed));

    [Test]
    public void Matches_legacy_when_stream_returns_short_reads()
    {
        var data = RandomBytes(100_003, 7);
        var expected = LegacyMurMurHash3.Hash(new MemoryStream(data), Seed);
        var actual = MurMurHash3.Hash(new TrickleStream(data, 3), Seed);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Allocates_close_to_nothing_whatever_the_stream_size()
    {
        var stream = new MemoryStream(RandomBytes(8 * 1024 * 1024, 9));
        MurMurHash3.Hash(stream, Seed); // warm the pool
        stream = new MemoryStream(stream.ToArray());

        var before = GC.GetAllocatedBytesForCurrentThread();
        MurMurHash3.Hash(stream, Seed);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.LessThan(1024));
    }

    /// <summary>Returns at most <c>maxPerRead</c> bytes per read to exercise carry handling.</summary>
    private sealed class TrickleStream(byte[] data, int maxPerRead) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, maxPerRead));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, maxPerRead)]);
    }
}
