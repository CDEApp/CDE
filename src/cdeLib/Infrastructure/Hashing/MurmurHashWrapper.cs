using System;
using MurmurHash.Net;

namespace cdeLib.Infrastructure.Hashing
{
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
    }
}