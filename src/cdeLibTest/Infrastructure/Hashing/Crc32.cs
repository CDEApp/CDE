using System;
using System.IO;
using cdeLib.Infrastructure.Hashing;

namespace cdeLibTest.Infrastructure.Hashing;

public class Crc32 : IHashAlgorithm
{
    private uint[] _tab;

    public Crc32()
    {
        Init(0x04c11db7);
    }

    public Crc32(uint poly)
    {
        Init(poly);
    }

    private void Init(uint poly)
    {
        _tab = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint t = i;
            for (int j = 0; j < 8; j++)
                if ((t & 1) == 0)
                    t >>= 1;
                else
                    t = (t >> 1) ^ poly;
            _tab[i] = t;
        }
    }

    public UInt64 Hash(byte[] data)
    {
        uint hash = 0xFFFFFFFF;
        foreach (byte b in data)
            hash = (hash << 8) ^ _tab[b ^ (hash >> 24)];
        return ~hash;
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