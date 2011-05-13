using System;

namespace cdeLib.Infrastructure.Hashing
{
    public interface IHashAlgorithm
    {
        UInt32 Hash(Byte[] data);
    }
    public interface ISeededHashAlgorithm : IHashAlgorithm
    {
        UInt32 Hash(Byte[] data, UInt32 seed);
    }
}