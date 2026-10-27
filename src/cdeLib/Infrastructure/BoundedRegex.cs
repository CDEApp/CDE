using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace cdeLib.Infrastructure;

/// <summary>
/// A user-supplied search regex with a match timeout, so a catastrophic-backtracking pattern
/// (ReDoS) cannot hang a search. Behaviour on timeout: the entry being tested is treated as a
/// non-match and the search continues; after <see cref="MaxTimeouts"/> timeouts the pattern is
/// deemed pathological and <see cref="Exhausted"/> becomes true - callers stop scanning (and any
/// further <see cref="IsMatch(ReadOnlySpan{char})"/> returns false immediately). Worst case a search
/// spends roughly <c>MaxTimeouts * MatchTimeout</c> in the regex engine.
/// Instances are cheap and per-search; IsMatch is allocation-free and thread-safe.
/// </summary>
public sealed class BoundedRegex
{
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);
    public const int MaxTimeouts = 3;

    private readonly Regex _regex;
    private int _timeouts;

    public BoundedRegex(string pattern)
        : this(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, MatchTimeout))
    {
    }

    /// <summary>Wrap an existing regex (which must have been constructed with a match timeout).</summary>
    public BoundedRegex(Regex regex)
    {
        _regex = regex ?? throw new ArgumentNullException(nameof(regex));
    }

    /// <summary>True once <see cref="MaxTimeouts"/> matches have timed out.</summary>
    public bool Exhausted => Volatile.Read(ref _timeouts) >= MaxTimeouts;

    public bool IsMatch(ReadOnlySpan<char> input)
    {
        if (Exhausted) return false;
        try
        {
            return _regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            Interlocked.Increment(ref _timeouts);
            return false;
        }
    }

    public bool IsMatch(string input)
    {
        if (Exhausted) return false;
        try
        {
            return _regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            Interlocked.Increment(ref _timeouts);
            return false;
        }
    }
}
