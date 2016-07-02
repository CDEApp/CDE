using System;
using System.Security.Cryptography;
using cdeLib.Infrastructure.Hashing;

namespace cdeLibTest.Infrastructure
{
    public class Sha1Wrapper : IHashAlgorithm
    {
        private readonly SHA1 _sha1;
        public Sha1Wrapper()
        {
            _sha1 = SHA1.Create();
        }


        public uint Hash(byte[] data)
        {
            var result = _sha1.ComputeHash(data);
            return BitConverter.ToUInt32(result,0);
        }
    }
}