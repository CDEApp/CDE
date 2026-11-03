using System;
using System.Buffers;

namespace cdeLib;

/// <summary>
/// A voidtools wildcard pattern, parsed once. <c>*</c> is zero or more characters except <c>\</c>, <c>**</c> is zero
/// or more characters including <c>\</c> (a run of three or more <c>*</c> is the same), and <c>?</c> is exactly one
/// character except <c>\</c>. Everything else is a literal, compared case-insensitively (ordinal, per UTF-16 char).
/// <see cref="IsMatch"/> tests the WHOLE text, so <c>*.txt</c> is a suffix match. It runs a dynamic program over
/// the text, O(text * pattern) in the worst case with no backtracking.
/// </summary>
public sealed class WildcardPattern
{
    private const int StackLimit = 256;
    private const char Backslash = (char)92;

    private enum Kind : byte { Literal, AnyChar, Star, DoubleStar }

    private readonly record struct Token(Kind Kind, char Folded);

    private readonly Token[] _tokens;
    private readonly int _minLength; // characters every match must contain at least

    private WildcardPattern(Token[] tokens)
    {
        _tokens = tokens;
        foreach (var t in tokens)
            if (t.Kind is Kind.Literal or Kind.AnyChar) _minLength++;
    }

    /// <summary>True if <paramref name="term"/> has a <c>*</c> or <c>?</c>, so it is a wildcard rather than a plain substring.</summary>
    public static bool IsWildcard(string term) => term.AsSpan().IndexOfAny('*', '?') >= 0;

    /// <summary>Parse a pattern. Any text is valid: every character that is not <c>*</c> or <c>?</c> is a literal.</summary>
    public static WildcardPattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var tokens = new Token[pattern.Length];
        var count = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            switch (pattern[i])
            {
                case '?':
                    tokens[count++] = new Token(Kind.AnyChar, '?');
                    break;
                case '*':
                    var run = 1;
                    while (i + run < pattern.Length && pattern[i + run] == '*') run++;
                    tokens[count++] = new Token(run == 1 ? Kind.Star : Kind.DoubleStar, '*');
                    i += run - 1;
                    break;
                default:
                    tokens[count++] = new Token(Kind.Literal, Fold(pattern[i]));
                    break;
            }
        }

        return new WildcardPattern(tokens.AsSpan(0, count).ToArray());
    }

    private static char Fold(char c) => char.ToUpperInvariant(c);

    /// <summary>True if the whole of <paramref name="text"/> matches the pattern.</summary>
    public bool IsMatch(ReadOnlySpan<char> text)
    {
        var n = text.Length;
        if (n < _minLength) return false;

        bool[] rented = null;
        Span<bool> row = n < StackLimit
            ? stackalloc bool[n + 1]
            : (rented = ArrayPool<bool>.Shared.Rent(n + 1)).AsSpan(0, n + 1);
        try
        {
            row.Clear();
            row[0] = true; // the empty pattern matches the empty text
            foreach (var token in _tokens)
            {
                if (!Step(token, text, row)) return false;
            }

            return row[n];
        }
        finally
        {
            if (rented is not null) ArrayPool<bool>.Shared.Return(rented);
        }
    }

    // row[j] says "the tokens so far match text[0..j)". Moves it forward by one token in place; false if no prefix survives.
    private static bool Step(Token token, ReadOnlySpan<char> text, Span<bool> row)
    {
        var n = text.Length;
        var any = false;
        switch (token.Kind)
        {
            case Kind.Literal:
                for (var j = n; j >= 1; j--)
                {
                    row[j] = row[j - 1] && Fold(text[j - 1]) == token.Folded;
                    any |= row[j];
                }

                row[0] = false;
                return any;
            case Kind.AnyChar:
                for (var j = n; j >= 1; j--)
                {
                    row[j] = row[j - 1] && text[j - 1] != Backslash;
                    any |= row[j];
                }

                row[0] = false;
                return any;
            case Kind.Star:
                any = row[0];
                for (var j = 1; j <= n; j++)
                {
                    row[j] = row[j] || (row[j - 1] && text[j - 1] != Backslash);
                    any |= row[j];
                }

                return any;
            default: // DoubleStar
                any = row[0];
                for (var j = 1; j <= n; j++)
                {
                    row[j] = row[j] || row[j - 1];
                    any |= row[j];
                }

                return any;
        }
    }
}
