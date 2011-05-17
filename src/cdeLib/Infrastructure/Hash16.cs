using System;
using ProtoBuf;

namespace cdeLib.Infrastructure
{
    [ProtoContract]
    public struct Hash16
    {
        [ProtoMember(1, IsRequired = true)]
        public ulong HashA; // first 8 bytes
        [ProtoMember(2, IsRequired = true)]
        public ulong HashB; // last 8 bytes

        public Hash16(byte[] hash)
        {
            HashA = BitConverter.ToUInt64(hash, 0); // swapped offset because of intel
            HashB = BitConverter.ToUInt64(hash, 8); // swapped offset because of intel
        }

        public string HashAsString
        {
            get
            {
                var a = BitConverter.GetBytes(HashA);
                var b = BitConverter.GetBytes(HashB);
                return ByteArrayHelper.ByteArrayToString(a) 
                       + ByteArrayHelper.ByteArrayToString(b);
            }
        }
    }
}