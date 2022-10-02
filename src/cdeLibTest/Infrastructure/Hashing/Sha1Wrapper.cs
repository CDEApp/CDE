using System;
using System.IO;
using System.Security.Cryptography;
using cdeLib.Infrastructure.Hashing;

namespace cdeLibTest.Infrastructure.Hashing;

public class Sha1Wrapper : IHashAlgorithm
{
    private readonly SHA1 _sha1;
    public Sha1Wrapper()
    {
        _sha1 = SHA1.Create();
    }

    public UInt64 Hash(byte[] data)
    {
        var result = _sha1.ComputeHash(data);
        return BitConverter.ToUInt64(result, 0);
    }

    public UInt64 Hash(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    public ulong HashStream(Stream stream)
    {
        throw new NotImplementedException();
    }
}