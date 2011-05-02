using System;
using System.Security.Cryptography;
using cdeLib.Infrastructure;

namespace cdeLibTest.Infrastructure
{
    public class MD5Hash : IHashAlgorithm
    {
        public uint Hash(byte[] data)
        {
            MD5 md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}