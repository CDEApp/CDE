using System;
using System.IO;
using System.Security.Cryptography;

namespace cdeLib.Infrastructure.Hashing;

public class MD5Hash : IHashAlgorithm
{
    public UInt64 Hash(byte[] data)
    {
        return BitConverter.ToUInt64(MD5.HashData(data), 0);
    }

    public UInt64 Hash(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[16]; // MD5 produces 16 bytes
        MD5.HashData(data, hash);
        return BitConverter.ToUInt64(hash);
    }

    public ulong HashStream(Stream stream)
    {
        throw new NotImplementedException();
    }
}