using System;
using System.Collections.Generic;
using Serilog;

namespace cdeLib.Entities;

/// <summary>
/// Decides whether a directory found during a scan is user-excluded. Patterns are absolute paths; a match
/// covers that folder and everything under it. '*' matches within a single path segment (no '**').
/// Case-sensitive except on Windows, where '/' and '\' are also interchangeable. Matching is done on
/// segments in place, so checking a path allocates nothing.
/// </summary>
public sealed class ScanExcludeMatcher
{
    public static readonly ScanExcludeMatcher None = new(Array.Empty<string>(), OperatingSystem.IsWindows());

    private readonly string[][] _patterns;
    private readonly bool _isWindows;

    /// <param name="patterns">Absolute path patterns; blank entries are skipped, non-absolute ones are logged and skipped.</param>
    /// <param name="isWindows">Selects the case and separator rules; injectable so every OS's rules can be tested anywhere.</param>
    public ScanExcludeMatcher(IEnumerable<string> patterns, bool isWindows)
    {
        _isWindows = isWindows;
        var list = new List<string[]>();
        foreach (var raw in patterns ?? Array.Empty<string>())
        {
            var pattern = raw?.Trim();
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            if (!IsAbsolute(pattern, isWindows))
            {
                Log.Warning("Scan exclude path ignored, it is not an absolute path: {Pattern}", pattern);
                continue;
            }

            var segments = Split(pattern, isWindows);
            if (segments.Length > 0)
            {
                list.Add(segments);
            }
        }

        _patterns = list.ToArray();
    }

    /// <summary>Builds a matcher for the running OS.</summary>
    public static ScanExcludeMatcher ForCurrentOs(IEnumerable<string> patterns)
    {
        return new ScanExcludeMatcher(patterns, OperatingSystem.IsWindows());
    }

    public bool IsEmpty => _patterns.Length == 0;

    /// <summary>True when <paramref name="fullPath"/> is, or is under, an excluded folder.</summary>
    public bool IsExcluded(string fullPath)
    {
        if (_patterns.Length == 0 || string.IsNullOrEmpty(fullPath))
        {
            return false;
        }

        foreach (var pattern in _patterns)
        {
            if (MatchesPrefix(fullPath.AsSpan(), pattern))
            {
                return true;
            }
        }

        return false;
    }

    // Absolute: "/..." on any OS (so Linux patterns can be tested on Windows), plus "C:\" and UNC "\\" on Windows.
    private static bool IsAbsolute(string p, bool isWindows)
    {
        if (p[0] == '/')
        {
            return true;
        }

        if (!isWindows)
        {
            return false;
        }

        return (p.Length >= 3 && char.IsAsciiLetter(p[0]) && p[1] == ':' && (p[2] == '\\' || p[2] == '/'))
               || (p.Length >= 2 && p[0] == '\\' && p[1] == '\\');
    }

    private static bool IsSeparator(char c, bool isWindows) => c == '/' || (isWindows && c == '\\');

    private static string[] Split(string pattern, bool isWindows)
    {
        var segments = new List<string>();
        var start = 0;
        for (var i = 0; i <= pattern.Length; i++)
        {
            if (i == pattern.Length || IsSeparator(pattern[i], isWindows))
            {
                if (i > start)
                {
                    segments.Add(pattern.Substring(start, i - start));
                }

                start = i + 1;
            }
        }

        return segments.ToArray();
    }

    // True when the first pattern.Length segments of path each match the pattern's segments.
    private bool MatchesPrefix(ReadOnlySpan<char> path, string[] pattern)
    {
        var segmentIndex = 0;
        var start = 0;
        for (var i = 0; i <= path.Length && segmentIndex < pattern.Length; i++)
        {
            if (i < path.Length && !IsSeparator(path[i], _isWindows))
            {
                continue;
            }

            if (i > start)
            {
                if (!SegmentMatches(path.Slice(start, i - start), pattern[segmentIndex]))
                {
                    return false;
                }

                segmentIndex++;
            }

            start = i + 1;
        }

        return segmentIndex == pattern.Length;
    }

    private bool SegmentMatches(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        // Iterative '*' matching with single backtrack point; '*' never crosses a segment as text is one segment.
        int t = 0, p = 0, starP = -1, starT = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                starP = p++;
                starT = t;
            }
            else if (p < pattern.Length && CharEquals(pattern[p], text[t]))
            {
                p++;
                t++;
            }
            else if (starP >= 0)
            {
                p = starP + 1;
                t = ++starT;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    private bool CharEquals(char a, char b)
    {
        return a == b || (_isWindows && char.ToUpperInvariant(a) == char.ToUpperInvariant(b));
    }
}
