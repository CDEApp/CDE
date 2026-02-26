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
        using var md5 = MD5.Create();
        Span<byte> hash = stackalloc byte[16]; // MD5 produces 16 bytes
        if (!md5.TryComputeHash(data, hash, out _))
        {
            throw new InvalidOperationException("Failed to compute MD5 hash");
        }
        return BitConverter.ToUInt64(hash);
    }

    public ulong HashStream(Stream stream)
    {
        throw new NotImplementedException();
    }
}