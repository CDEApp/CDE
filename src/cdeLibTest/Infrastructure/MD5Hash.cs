using System;
using System.Security.Cryptography;
using cdeLib.Infrastructure.Hashing;

namespace cdeLibTest.Infrastructure
{
    public class MD5Hash : IHashAlgorithm
    {
        public UInt64 Hash(byte[] data)
        {
            var md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            return BitConverter.ToUInt64(hash, 0);
        }

        public UInt64 Hash(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }
    }
}