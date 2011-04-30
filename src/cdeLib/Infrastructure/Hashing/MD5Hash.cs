using System;
using System.Security.Cryptography;

namespace cdeLib.Infrastructure
{
    public class MD5Hash : IHashAlgorithm
    {
        public uint Hash(byte[] data)
        {
            var md5 = MD5.Create();
            return BitConverter.ToUInt32(md5.ComputeHash(data), 0);
        }
    }
}