using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Serilog;

namespace cdeLib.Infrastructure;

/// <summary>
/// A user-supplied search regex with a match timeout, so a catastrophic-backtracking pattern
/// (ReDoS) cannot hang a search. Behaviour on timeout: the entry being tested is treated as a
/// non-match and the search continues; after <see cref="MaxTimeouts"/> timeouts the pattern is
/// deemed pathological and <see cref="Exhausted"/> becomes true - callers stop scanning (and any
/// further <c>IsMatch</c> returns false immediately) and call <see cref="ReportIfExhausted"/> so the
/// truncation is never silent (one Serilog warning per search). Worst case a search spends roughly
/// <c>MaxTimeouts * MatchTimeout</c> in the regex engine.
/// The compiled <see cref="Regex"/> (timeout baked in) is cached per pattern and shared; the timeout
/// counter is per instance, i.e. per search. <c>IsMatch</c> is allocation-free and thread-safe.
/// </summary>
public sealed class BoundedRegex
{
    // Wall-clock: generous so a loaded machine or a GC pause does not trip a legitimate pattern.
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    public const int MaxTimeouts = 3;

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;
    private const int MaxCacheSize = 100; // prevent unbounded growth
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    private readonly Regex _regex;
    private int _timeouts;
    private int _reported;

    /// <summary>Search regex for <paramref name="pattern"/> using the shared compiled-regex cache.</summary>
    public BoundedRegex(string pattern)
        : this(GetCached(pattern))
    {
    }

    /// <summary>Wrap an existing regex, which must have a finite match timeout.</summary>
    public BoundedRegex(Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);
        if (regex.MatchTimeout == Regex.InfiniteMatchTimeout)
            throw new ArgumentException("Regex must have a match timeout.", nameof(regex));
        _regex = regex;
    }

    public string Pattern => _regex.ToString();

    /// <summary>Number of matches that timed out so far.</summary>
    public int Timeouts => Volatile.Read(ref _timeouts);

    /// <summary>True once <see cref="MaxTimeouts"/> matches have timed out.</summary>
    public bool Exhausted => Volatile.Read(ref _timeouts) >= MaxTimeouts;

    /// <summary>
    /// If exhausted, logs (once per instance) that the search stopped early and its results are
    /// incomplete. Returns <see cref="Exhausted"/>.
    /// </summary>
    public bool ReportIfExhausted()
    {
        if (!Exhausted) return false;
        if (Interlocked.Exchange(ref _reported, 1) == 0)
        {
            Log.Warning(
                "Regex search stopped early, results are incomplete: pattern {Pattern} timed out {Timeouts} times (limit {Timeout} each)",
                Pattern, Timeouts, MatchTimeout);
        }
        return true;
    }

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

    private static Regex GetCached(string pattern)
    {
        if (Cache.TryGetValue(pattern, out var hit)) return hit;

        if (Cache.Count > MaxCacheSize)
        {
            // Evict ~20% (arbitrary keys) to avoid frequent evictions.
            foreach (var key in Cache.Keys.Take(MaxCacheSize / 5).ToArray())
                Cache.TryRemove(key, out _);
        }
        return Cache.GetOrAdd(pattern, p => new Regex(p, Options, MatchTimeout));
    }
}
