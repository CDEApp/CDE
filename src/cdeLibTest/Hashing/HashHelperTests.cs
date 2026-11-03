using System;
using System.IO;
using System.Linq;
using System.IO.Hashing;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure.Hashing;
using NSubstitute;
using NUnit.Framework;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

[NonParallelizable]
public class HashHelperTests
{
    private const int Chunk = HashHelper.FullHashChunkSize;

    private static Hash16 Expected(ReadOnlySpan<byte> bytes) => new(XxHash128.HashToUInt128(bytes));

    [Test]
    public void Hash16_from_UInt128_puts_the_high_half_in_HashA()
    {
        var value = new UInt128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL);

        Assert.That(new Hash16(value), Is.EqualTo(new Hash16(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL)));
    }

    // Small first-pass size so multi-block files stay tiny; 65536 is the production default.
    [TestCase(1024)]
    [TestCase(65536)]
    public async Task Partial_hash_is_hash_of_exactly_first_N_bytes_for_all_size_classes(int firstPass)
    {
        foreach (var size in SizeClasses(firstPass))
        {
            await AssertHash(size, firstPass);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1000)]
    public async Task Full_hash_of_a_file_within_one_chunk_is_the_hash_of_its_bytes(int size)
    {
        await AssertHash(size, null);
    }

    [Test]
    public async Task Full_hash_across_the_chunk_boundary_equals_the_one_shot_hash([Range(-1, 1)] int delta)
    {
        await AssertHash(Chunk + delta, null);
    }

    [Test]
    public async Task Full_hash_of_several_chunks_and_a_tail_equals_the_one_shot_hash()
    {
        await AssertHash(3 * Chunk + 5, null);
    }

    [TestCase("", "99aa06d3014798d86001c324468d497f")]
    [TestCase("abc", "06b05ab6733a618578af5f94892f3950")]
    [TestCase("bytes 0..255 repeated 4097 times", "3898db5d5b4a87468b544a01c6d30e1c")]
    public async Task Full_hash_matches_the_libxxhash_known_answer(string content, string expectedHex)
    {
        var bytes = content switch
        {
            "" => [],
            "abc" => "abc"u8.ToArray(),
            _ => Enumerable.Range(0, 256 * 4097).Select(i => (byte)i).ToArray(),
        };
        var path = Path.Combine(Path.GetTempPath(), "cde-hashhelper-kat-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, bytes);

            var response = await new HashHelper(Substitute.For<ILogger>()).GetHashResponseFromFile(path, null);

            Assert.That(response.Hash.HashA.ToString("x16") + response.Hash.HashB.ToString("x16"), Is.EqualTo(expectedHex));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Unopenable_file_is_a_failed_hash()
    {
        var helper = new HashHelper(Substitute.For<ILogger>());
        var missing = Path.Combine(Path.GetTempPath(), "cde-no-such-file-" + Guid.NewGuid().ToString("N"));

        Assert.That((await helper.GetHashResponseFromFile(missing, 1024)).IsSuccess, Is.False);
        Assert.That((await helper.GetHashResponseFromFile(missing, null)).IsSuccess, Is.False);
    }

    private static int[] SizeClasses(int n) =>
    [
        0, 1, n - 1, n, n + 1, n + n / 2, 2 * n - 1, 2 * n, 2 * n + 1, 3 * n, 3 * n + 5
    ];

    private static async Task AssertHash(int size, int? bytesToHash)
    {
        var path = Path.Combine(Path.GetTempPath(), "cde-hashhelper-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = new byte[size];
            new Random(size).NextBytes(data);
            await File.WriteAllBytesAsync(path, data);

            var hashed = Math.Min(size, bytesToHash ?? size);
            var actual = await new HashHelper(Substitute.For<ILogger>()).GetHashResponseFromFile(path, bytesToHash);

            var label = $"size {size}, bytesToHash {bytesToHash?.ToString() ?? "full"}";
            Assert.That(actual.IsSuccess, Is.True, label);
            Assert.That(actual.Hash, Is.EqualTo(Expected(data.AsSpan(0, hashed))), label);
            Assert.That(actual.IsPartialHash, Is.EqualTo(size > bytesToHash), label);
            Assert.That(actual.BytesHashed, Is.EqualTo(hashed), label);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
