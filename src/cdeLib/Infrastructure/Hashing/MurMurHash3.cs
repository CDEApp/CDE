using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;

namespace cdeLib.Infrastructure.Hashing;

/*
This code is public domain.
The MurmurHash3 algorithm was created by Austin Appleby and put into the public domain.  See http://code.google.com/p/smhasher/
This C# variant was authored by
Elliott B. Edwards and was placed into the public domain as a gist
Status...Working on verification (Test Suite)
Set up to run as a LinqPad (linqpad.net) script (thus the ".Dump()" call)
*/
public static class MurMurHash3
{
    private const uint C1 = 0xcc9e2d51;
    private const uint C2 = 0x1b873593;

    //Change to suit your needs
    //const uint seed = 144;

    /// <summary>
    /// Compute MurmurHash3 for in-memory data using ReadOnlySpan (zero-allocation)
    /// </summary>
    public static uint Hash(ReadOnlySpan<byte> data, uint seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        uint h1 = seed;
        int length = data.Length;
        int position = 0;

        // Process 4-byte chunks
        while (position + 4 <= length)
        {
            uint k1 = (uint)(data[position]
                           | data[position + 1] << 8
                           | data[position + 2] << 16
                           | data[position + 3] << 24);

            k1 *= c1;
            k1 = rotl32(k1, 15);
            k1 *= c2;

            h1 ^= k1;
            h1 = rotl32(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;

            position += 4;
        }

        // Handle remaining bytes (0-3)
        int remaining = length - position;
        if (remaining > 0)
        {
            uint k1 = 0;
            switch (remaining)
            {
                case 3:
                    k1 = (uint)(data[position]
                              | data[position + 1] << 8
                              | data[position + 2] << 16);
                    break;
                case 2:
                    k1 = (uint)(data[position]
                              | data[position + 1] << 8);
                    break;
                case 1:
                    k1 = data[position];
                    break;
            }

            if (k1 != 0)
            {
                k1 *= c1;
                k1 = rotl32(k1, 15);
                k1 *= c2;
                h1 ^= k1;
            }
        }

        // Finalization
        h1 ^= (uint)length;
        h1 = fmix(h1);

        unchecked
        {
            return h1;
        }
    }

    /// <summary>Size of the pooled buffer used when hashing a stream.</summary>
    public const int StreamChunkSize = 1024 * 1024;

    /// <summary>
    /// Incremental MurmurHash3 (x86_32) state. Feed bytes with <see cref="Append"/> in any slicing;
    /// the 0-3 bytes left over after a call are carried into the next one. Gives the same result as
    /// hashing the concatenated bytes in one go.
    /// </summary>
    public struct Incremental
    {
        private uint _h1;
        private uint _length;
        private uint _carry;
        private int _carryCount;

        public Incremental(uint seed)
        {
            _h1 = seed;
            _length = 0;
            _carry = 0;
            _carryCount = 0;
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            unchecked { _length += (uint)data.Length; }
            var i = 0;

            // Complete a block begun by a previous call.
            if (_carryCount > 0)
            {
                while (_carryCount < 4 && i < data.Length)
                {
                    _carry |= (uint)data[i++] << (8 * _carryCount);
                    _carryCount++;
                }

                if (_carryCount < 4) return;
                MixBlock(_carry);
                _carry = 0;
                _carryCount = 0;
            }

            for (; i + 4 <= data.Length; i += 4)
            {
                MixBlock(BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4)));
            }

            while (i < data.Length)
            {
                _carry |= (uint)data[i++] << (8 * _carryCount);
                _carryCount++;
            }
        }

        public uint Finish()
        {
            var h1 = _h1;
            if (_carryCount > 0)
            {
                var k1 = _carry;
                k1 *= C1;
                k1 = rotl32(k1, 15);
                k1 *= C2;
                h1 ^= k1;
            }

            h1 ^= _length;
            return fmix(h1);
        }

        private void MixBlock(uint k1)
        {
            k1 *= C1;
            k1 = rotl32(k1, 15);
            k1 *= C2;

            _h1 ^= k1;
            _h1 = rotl32(_h1, 13);
            _h1 = _h1 * 5 + 0xe6546b64;
        }
    }

    /// <summary>
    /// Hash a stream by reading it in <see cref="StreamChunkSize"/> chunks into one pooled buffer.
    /// Result is identical to the original 4-bytes-at-a-time implementation (hashes are persisted in catalogs).
    /// Disposes the stream, as the original did.
    /// </summary>
    public static uint Hash(Stream stream, UInt32 seed)
    {
        var state = new Incremental(seed);
        var buffer = ArrayPool<byte>.Shared.Rent(StreamChunkSize);
        try
        {
            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer, 0, StreamChunkSize)) > 0)
                {
                    state.Append(buffer.AsSpan(0, read));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return state.Finish();
    }

    private static uint rotl32(uint x, byte r)
    {
        return (x << r) | (x >> (32 - r));
    }

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