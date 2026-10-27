using System;
using System.Threading.Tasks;
using cdeLib.Infrastructure.Hashing;
using MurmurHash.Net;
using NUnit.Framework;

namespace cdeLibTest.Hashing;

/// <summary>Issue #37: the read loop behind the partial hash must tolerate short reads.</summary>
public class PartialHashFirstBlockTests
{
    private static byte[] Data(int size)
    {
        var data = new byte[size];
        new Random(size).NextBytes(data);
        return data;
    }

    private static ValueTask<int> ReadChunked(byte[] source, Memory<byte> dest, long offset, int maxPerRead)
    {
        var n = (int)Math.Min(Math.Min(dest.Length, maxPerRead), Math.Max(0, source.Length - offset));
        source.AsMemory((int)offset, n).CopyTo(dest);
        return new ValueTask<int>(n);
    }

    [TestCase(1000, 1024, 1)]
    [TestCase(1024, 1024, 7)]
    [TestCase(1025, 1024, 7)]
    [TestCase(5000, 1024, 100)]
    [TestCase(5000, 1024, 1023)]
    [TestCase(5000, 1024, 5000)]
    public async Task Short_reads_fill_the_same_first_N_bytes_as_full_reads(int fileSize, int n, int maxPerRead)
    {
        var data = Data(fileSize);
        var buffer = new byte[n];

        var filled = await HashHelper.FillBufferAsync((dest, off) => ReadChunked(data, dest, off, maxPerRead), buffer);

        var expectedLen = Math.Min(fileSize, n);
        Assert.That(filled, Is.EqualTo(expectedLen));
        Assert.That(buffer.AsSpan(0, filled).ToArray(), Is.EqualTo(data.AsSpan(0, expectedLen).ToArray()));
        Assert.That(MurmurHash3.Hash32(buffer.AsSpan(0, filled), 123456U),
            Is.EqualTo(MurmurHash3.Hash32(data.AsSpan(0, expectedLen), 123456U)));
    }

    [Test]
    public async Task Fill_never_reads_past_the_buffer_length()
    {
        var data = Data(5000);
        long maxEnd = 0;
        var buffer = new byte[1024];

        await HashHelper.FillBufferAsync((dest, off) =>
        {
            maxEnd = Math.Max(maxEnd, off + dest.Length);
            return ReadChunked(data, dest, off, 300);
        }, buffer);

        Assert.That(maxEnd, Is.EqualTo(1024));
    }
}
