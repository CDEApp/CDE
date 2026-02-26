using System;

namespace cdeLib.Infrastructure;

public static class ByteArrayHelper
{
    // Lowercase hex digit lookup
    private static ReadOnlySpan<byte> HexCharsLower => "0123456789abcdef"u8;
    /// <summary>
    /// Converts a byte array to a lowercase hex string.
    /// Optimized to output lowercase directly without intermediate allocation.
    /// </summary>
    public static string ByteArrayToString(byte[] bytes)
    {
        if (bytes == null)
        {
            return "null";
        }
        return ByteArrayToString(bytes.AsSpan());
    }

    /// <summary>
    /// Converts a byte span to a lowercase hex string.
    /// Optimized to output lowercase directly without intermediate allocation.
    /// </summary>
    public static string ByteArrayToString(ReadOnlySpan<byte> bytes)
    {
        return string.Create(bytes.Length * 2, bytes, (chars, source) =>
        {
            var hexChars = HexCharsLower;
            for (int i = 0; i < source.Length; i++)
            {
                byte b = source[i];
                chars[i * 2] = (char)hexChars[b >> 4];
                chars[i * 2 + 1] = (char)hexChars[b & 0xF];
            }
        });
    }

    /// <summary>
    /// Converts a ulong to a lowercase hex string (16 chars, little-endian byte order).
    /// Optimized to output lowercase directly without intermediate allocation.
    /// </summary>
    public static string ULongToHexString(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(bytes, value);
        return ByteArrayToString(bytes);
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
        return ByteArrayToString(bytes);
    }
}