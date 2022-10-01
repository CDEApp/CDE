using System;
using System.IO;
using System.Security.Cryptography;

namespace cdeLib.Infrastructure.Hashing;

public class MD5Hash : IHashAlgorithm
{
    public UInt64 Hash(byte[] data)
    {
        var md5 = MD5.Create();
        return BitConverter.ToUInt64(md5.ComputeHash(data), 0);
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