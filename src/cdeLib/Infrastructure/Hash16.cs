using System;
using System.Collections.Generic;
using System.Diagnostics;
using ProtoBuf;

namespace cdeLib.Infrastructure
{
    [DebuggerDisplay("HashA = {HashA}, HashB = {HashB}")]
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

        public Hash16(int hash)
        {
            HashA = 0;
            HashB = (ulong)hash;
        }

        public void SetHash(byte[] hash)
        {
            HashA = BitConverter.ToUInt64(hash, 0); // swapped offset because of intel
            HashB = BitConverter.ToUInt64(hash, 8); // swapped offset because of intel
        }

        public void SetHash(int hash)
        {
            HashA = 0;
            HashB = (ulong)hash;
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

        public override string ToString()
        {
            return $"A:[{HashA}] B:[{HashB}]";
        }

        public class EqualityComparer: IEqualityComparer<Hash16>
        {
            public bool Equals(Hash16 x, Hash16 y)
            {
                //return x.HashA == y.HashA && x.HashB == y.HashB;
                return StaticEquals(x, y);
            }

            public int GetHashCode(Hash16 obj)
            {
                return StaticGetHashCode(obj);
            }

            public static bool StaticEquals(Hash16 x, Hash16 y)
            {
                return x.HashA == y.HashA && x.HashB == y.HashB;
            }

            public static int StaticGetHashCode(Hash16 obj)
            {
                // quite likely a bad choice for hash.
                return ((((int)(obj.HashA >> 32)) * 31 +
                       (int)(obj.HashA & 0xFFFFFFFF)) * 31 +
                       (int)(obj.HashB >> 32)) * 31 +
                       (int)(obj.HashB & 0xFFFFFFFF);
            }
        }
    }
}