using System;

namespace cdeLib.Infrastructure.Hashing
{
    public interface IHashAlgorithm
    {
        UInt64 Hash(Byte[] data);
        UInt64 Hash(ReadOnlySpan<byte> data);
    }
    public interface ISeededHashAlgorithm : IHashAlgorithm
    {
        UInt64 Hash(Byte[] data, UInt32 seed);
    }
}