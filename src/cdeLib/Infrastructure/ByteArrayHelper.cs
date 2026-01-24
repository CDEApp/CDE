using System;

namespace cdeLib.Infrastructure;

public static class ByteArrayHelper
{
    /// <summary>
    /// Converts a byte array to a lowercase hex string.
    /// Uses Convert.ToHexString which is highly optimized in .NET 5+.
    /// </summary>
    public static string ByteArrayToString(byte[] bytes)
    {
        if (bytes == null)
        {
            return "null";
        }
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a byte array to a lowercase hex string.
    /// Uses Convert.ToHexString which is highly optimized in .NET 5+.
    /// </summary>
    public static string ByteArrayToString(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a ulong to a lowercase hex string (16 chars, little-endian byte order).
    /// Avoids byte array allocation by using stackalloc.
    /// </summary>
    public static string ULongToHexString(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(bytes, value);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Converts two ulongs to a lowercase hex string (32 chars total).
    /// Optimized for Hash16 to avoid multiple allocations.
    /// </summary>
    public static string Hash16ToHexString(ulong hashA, ulong hashB)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, hashA);
        BitConverter.TryWriteBytes(bytes.Slice(8), hashB);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}