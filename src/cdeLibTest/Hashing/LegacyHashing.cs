using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using MurmurHash.Net;

namespace cdeLibTest.Hashing;

/// <summary>
/// Verbatim copies of the hashing implementations as they were before the allocation work
/// (issues #35 / #36). Used as oracles: persisted hashes in existing catalogs must never change.
/// Do not "improve" this file.
/// </summary>
public static class LegacyMurMurHash3
{
    public static uint Hash(Stream stream, UInt32 seed)
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
                        k1 = rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        h1 = rotl32(h1, 13);
                        h1 = h1 * 5 + 0xe6546b64;
                        break;
                    case 3:
                        k1 = (uint)(chunk[0] | chunk[1] << 8 | chunk[2] << 16);
                        k1 *= c1;
                        k1 = rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                    case 2:
                        k1 = (uint)(chunk[0] | chunk[1] << 8);
                        k1 *= c1;
                        k1 = rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                    case 1:
                        k1 = chunk[0];
                        k1 *= c1;
                        k1 = rotl32(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        break;
                }

                chunk = reader.ReadBytes(4);
            }
        }

        h1 ^= streamLength;
        h1 = fmix(h1);
        return h1;
    }

    private static uint rotl32(uint x, byte r) => (x << r) | (x >> (32 - r));

    private static uint fmix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}

/// <summary>Result shape of the legacy HashHelper.</summary>
public class LegacyHashResponse
{
    public byte[] Hash { get; set; }
    public bool IsPartialHash { get; set; }
    public long BytesHashed { get; set; }
}

public static class LegacyHashHelper
{
    private const UInt32 Seed = 123456U;

    public static async Task<LegacyHashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
    {
        var hashResponse = new LegacyHashResponse();
        try
        {
            await using Stream stream = File.OpenRead(filename);
            long totalBytesRead;
            var streamLength = stream.Length;
            if (bytesToHash == null)
            {
                totalBytesRead = streamLength;
                hashResponse.Hash = BitConverter.GetBytes((UInt64)LegacyMurMurHash3.Hash(stream, Seed));
            }
            else
            {
                byte[] rentedBuffer = null;
                try
                {
                    var bufferSize = bytesToHash.Value;
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    var buf = rentedBuffer.AsMemory(0, bufferSize);

                    var bytesRead = await stream.ReadAsync(buf);

                    totalBytesRead = bytesRead;
                    while (bytesRead > 0 && totalBytesRead <= bytesToHash)
                    {
                        bytesRead = await stream.ReadAsync(rentedBuffer.AsMemory(0, bufferSize));
                        totalBytesRead += bytesRead;
                    }

                    var actualBytesToHash = (int)Math.Min(totalBytesRead, bufferSize);
                    hashResponse.Hash = BitConverter.GetBytes((UInt64)MurmurHash3.Hash32(rentedBuffer.AsSpan(0, actualBytesToHash), Seed));
                }
                finally
                {
                    if (rentedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                }
            }

            hashResponse.BytesHashed = totalBytesRead;
            hashResponse.IsPartialHash = streamLength > bytesToHash;

            return hashResponse;
        }
        catch (FileLoadException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
