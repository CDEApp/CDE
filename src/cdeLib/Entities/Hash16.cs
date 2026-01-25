using System;
using System.Collections.Generic;
using System.Diagnostics;
using cdeLib.Infrastructure;
using FlatSharp.Attributes;
using MessagePack;
using ProtoBuf;

namespace cdeLib.Entities;

[DebuggerDisplay("HashA = {HashA}, HashB = {HashB}")]
[ProtoContract]
[FlatBufferStruct]
[MessagePackObject]
public struct Hash16
{
    [ProtoMember(1, IsRequired = true)]
    [FlatBufferItem(0)]
    [Key(0)]
    public ulong HashA { get; set; } // first 8 bytes

    [ProtoMember(2, IsRequired = true)]
    [FlatBufferItem(1)]
    [Key(1)]
    public ulong HashB { get; set; } // last 8 bytes

    /// <summary>
    /// Returns true if this hash has been set (non-zero value).
    /// Zero hash (both HashA and HashB are 0) is used as sentinel for "not set".
    /// </summary>
    [IgnoreMember]
    public bool IsSet => HashA != 0 || HashB != 0;

    /// <summary>
    /// Returns an empty/unset hash (all zeros).
    /// </summary>
    public static Hash16 Empty => default;

    public Hash16(byte[] hash)
    {
        SetHash(hash);
    }

    public Hash16(int hash)
    {
        HashA = 0;
        HashB = (ulong)hash;
    }

    private void SetHash(byte[] hash)
    {
        HashA = BitConverter.ToUInt64(hash, 0); // swapped offset because of intel
        HashB = hash.Length > 8 ? BitConverter.ToUInt64(hash, 8) : 0;
    }

    [IgnoreMember]
    public string HashAsString => ByteArrayHelper.Hash16ToHexString(HashA, HashB);

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
        return obj is Hash16 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(HashA, HashB);
    }

    public class EqualityComparer : IEqualityComparer<Hash16>
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
            return (((int)(obj.HashA >> 32) * 31 +
                     (int)(obj.HashA & 0xFFFFFFFF)) * 31 +
                    (int)(obj.HashB >> 32)) * 31 +
                   (int)(obj.HashB & 0xFFFFFFFF);
        }
    }
}