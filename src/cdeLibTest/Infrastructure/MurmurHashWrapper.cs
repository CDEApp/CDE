using System;
using cdeLib.Infrastructure.Hashing;
using MurmurHash.Net;

namespace cdeLibTest.Infrastructure
{
    public class MurmurHashWrapper : IHashAlgorithm
    {
        private const UInt32 Seed = 123456U;

        public uint Hash(byte[] data)
        {
            return MurmurHash3.Hash32(bytes: data, seed: Seed);
        }
    }
}