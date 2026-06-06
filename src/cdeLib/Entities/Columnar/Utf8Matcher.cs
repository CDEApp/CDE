using System;
using System.Text;

namespace cdeLib.Entities.Columnar;

/// <summary>
/// Ordinal, case-insensitive substring matcher over UTF-8 bytes, built once per query. The common
/// case — an ASCII pattern against an ASCII name — is matched by folding bytes in place with no
/// allocation. A pattern or name containing non-ASCII bytes falls back to a decoded
/// <see cref="StringComparison.OrdinalIgnoreCase"/> compare (allocates only for those rare names),
/// so results match the tree/store search path exactly.
/// </summary>
public readonly ref struct Utf8Matcher
{
    private readonly ReadOnlySpan<byte> _patternLowerAscii; // A-Z folded to a-z; valid only when _asciiPattern
    private readonly string _pattern;
    private readonly bool _asciiPattern;
    private readonly bool _empty;

    public Utf8Matcher(string pattern)
    {
        _pattern = pattern ?? string.Empty;
        _empty = _pattern.Length == 0;
        var bytes = _empty ? [] : Encoding.UTF8.GetBytes(_pattern);
        _asciiPattern = System.Text.Ascii.IsValid(bytes);
        if (_asciiPattern && !_empty)
        {
            for (var i = 0; i < bytes.Length; i++) bytes[i] = ToLower(bytes[i]);
        }
        _patternLowerAscii = bytes;
    }

    public bool Contains(ReadOnlySpan<byte> nameUtf8)
    {
        if (_empty) return true;
        if (_asciiPattern && System.Text.Ascii.IsValid(nameUtf8))
            return AsciiContainsFolded(nameUtf8, _patternLowerAscii);

        // Rare path: non-ASCII somewhere. Decode and compare with real ordinal-ignore-case.
        return Encoding.UTF8.GetString(nameUtf8).Contains(_pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AsciiContainsFolded(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needleLower)
    {
        if (haystack.Length < needleLower.Length) return false;
        var last = haystack.Length - needleLower.Length;
        for (var i = 0; i <= last; i++)
        {
            var k = 0;
            for (; k < needleLower.Length; k++)
            {
                if (ToLower(haystack[i + k]) != needleLower[k]) break;
            }
            if (k == needleLower.Length) return true;
        }
        return false;
    }

    private static byte ToLower(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b;
}
