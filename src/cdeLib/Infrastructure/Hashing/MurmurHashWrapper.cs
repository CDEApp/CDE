using System;
using System.IO;
using MurmurHash.Net;

namespace cdeLib.Infrastructure.Hashing;

public class MurmurHashWrapper : IHashAlgorithm
{
    private const UInt32 Seed = 123456U;

    public UInt64 Hash(byte[] data)
    {
        return MurmurHash3.Hash32(bytes: data, seed: Seed);
    }

    public UInt64 Hash(ReadOnlySpan<byte> data)
    {
        return MurmurHash3.Hash32(bytes: data, seed: Seed);
    }

    public UInt64 HashStream(Stream stream)
    {
        return MurMurHash3.Hash(stream, Seed);
    }
}