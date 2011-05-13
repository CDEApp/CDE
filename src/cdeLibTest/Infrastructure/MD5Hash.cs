using System;
using System.Security.Cryptography;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Hashing;

namespace cdeLibTest.Infrastructure
{
    public class MD5Hash : IHashAlgorithm
    {
        public uint Hash(byte[] data)
        {
            var md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}