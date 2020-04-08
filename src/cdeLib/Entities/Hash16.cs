using System;
using System.Collections.Generic;
using System.Diagnostics;
using FlatSharp.Attributes;
using ProtoBuf;
using MessagePack;

namespace cdeLib.Infrastructure
{
    [DebuggerDisplay("HashA = {HashA}, HashB = {HashB}")]
    [ProtoContract]
    [FlatBufferStruct]
    [MessagePackObject]
    public sealed class Hash16 : object
    {
        [ProtoMember(1, IsRequired = true)]
        [FlatBufferItem(0)]
        [Key(0)]
        public ulong HashA { get; set; } // first 8 bytes
        
        [ProtoMember(2, IsRequired = true)]
        [FlatBufferItem(1)]
        [Key(1)]
        public ulong HashB { get; set; } // last 8 bytes

        public Hash16()
        {
        }

        public Hash16(byte[] hash)
        {
            SetHash(hash);
        }

        public Hash16(int hash)
        {
            HashA = 0;
            HashB = (ulong)hash;
        }

        public void SetHash(byte[] hash)
        {
            HashA = BitConverter.ToUInt64(hash, 0); // swapped offset because of intel
            HashB = hash.Length > 8 ? BitConverter.ToUInt64(hash, 8) : (ulong) 0;
        }

        public void SetHash(int hash)
        {
            HashA = 0;
            HashB = (ulong)hash;
        }

        [Key(2)]
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

        private bool Equals(Hash16 other)
        {
            return HashA == other.HashA && HashB == other.HashB;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is Hash16 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashA, HashB);
        }

        public class EqualityComparer: IEqualityComparer<Hash16>
        {
            public bool Equals(Hash16 x, Hash16 y)
            {
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