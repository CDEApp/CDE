using System;
using System.IO;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure.Hashing;
using NSubstitute;
using NUnit.Framework;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #36: per-file hashing must not change any hash value. The legacy implementation
/// (including the partial-hash "second block" quirk tracked by #37) is the oracle.
/// </summary>
[NonParallelizable]
public class HashHelperTests
{
    // Small first-pass size so multi-block files stay tiny; 65536 is the production default.
    [TestCase(1024)]
    [TestCase(65536)]
    public async Task Partial_hash_matches_legacy_for_all_size_classes(int firstPass)
    {
        foreach (var size in SizeClasses(firstPass))
        {
            await AssertSameAsLegacy(size, firstPass);
        }
    }

    [Test]
    public async Task Full_hash_matches_legacy_for_all_size_classes()
    {
        foreach (var size in SizeClasses(65536))
        {
            await AssertSameAsLegacy(size, null);
        }
    }

    [Test]
    public async Task Full_hash_matches_legacy_across_chunk_boundary([Range(-3, 3)] int delta)
    {
        await AssertSameAsLegacy(MurMurHash3.StreamChunkSize + delta, null);
    }

    [Test]
    public async Task Unopenable_file_is_a_failed_hash()
    {
        var helper = new HashHelper(Substitute.For<ILogger>());
        var missing = Path.Combine(Path.GetTempPath(), "cde-no-such-file-" + Guid.NewGuid().ToString("N"));

        Assert.That((await helper.GetHashResponseFromFile(missing, 1024)).IsSuccess, Is.False);
        Assert.That((await helper.GetHashResponseFromFile(missing, null)).IsSuccess, Is.False);
    }

    [Test]
    public void SetHash_from_ulong_equals_SetHash_from_bytes()
    {
        const ulong value = 0xDEADBEEFUL;
        var viaBytes = new DirEntry(false);
        viaBytes.SetHash(BitConverter.GetBytes(value));
        var viaUlong = new DirEntry(false);
        viaUlong.SetHash(value);

        Assert.That(viaUlong.Hash, Is.EqualTo(viaBytes.Hash));
        Assert.That(viaUlong.IsHashDone, Is.True);
    }

    private static int[] SizeClasses(int n) =>
    [
        0, 1, n - 1, n, n + 1, n + n / 2, 2 * n - 1, 2 * n, 2 * n + 1, 3 * n, 3 * n + 5
    ];

    private static async Task AssertSameAsLegacy(int size, int? bytesToHash)
    {
        var path = Path.Combine(Path.GetTempPath(), "cde-hashhelper-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = new byte[size];
            new Random(size).NextBytes(data);
            await File.WriteAllBytesAsync(path, data);

            var expected = await LegacyHashHelper.GetHashResponseFromFile(path, bytesToHash);
            var actual = await new HashHelper(Substitute.For<ILogger>()).GetHashResponseFromFile(path, bytesToHash);

            var label = $"size {size}, bytesToHash {bytesToHash?.ToString() ?? "full"}";
            Assert.That(actual.IsSuccess, Is.True, label);
            Assert.That(actual.Hash, Is.EqualTo(BitConverter.ToUInt64(expected.Hash, 0)), label);
            Assert.That(actual.IsPartialHash, Is.EqualTo(expected.IsPartialHash), label);
            Assert.That(actual.BytesHashed, Is.EqualTo(expected.BytesHashed), label);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
