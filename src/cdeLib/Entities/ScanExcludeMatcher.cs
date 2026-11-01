using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib.Infrastructure.Config;
using Serilog;

namespace cdeLib.Entities;

/// <summary>
/// Decides whether a directory found during a scan is user-excluded. A pattern is either an absolute path, or a bare
/// name with no path separator (such as "node_modules") that matches a folder of that name at any depth below the scan
/// root; a match covers that folder and everything under it (the scan never descends into it, so only an entry's own name is checked). '*' matches within a single path segment (no **).
/// Case-sensitive except on Windows, where '/' and '\' are also interchangeable. Matching is done on
/// segments in place, so checking a path allocates nothing.
/// </summary>
public sealed class ScanExcludeMatcher
{
    public static readonly ScanExcludeMatcher None = new(Array.Empty<string>(), OperatingSystem.IsWindows());

    private readonly string[][] _patterns;
    private readonly string[] _bareNames;
    private readonly int _bareStart; // chars of the scan root; bare names are only checked after it so the root is never excluded
    private readonly bool _isWindows;

    private ScanExcludeMatcher(string[][] patterns, string[] bareNames, int bareStart, bool isWindows)
    {
        _patterns = patterns;
        _bareNames = bareNames;
        _bareStart = bareStart;
        _isWindows = isWindows;
    }

    /// <param name="patterns">Absolute path patterns or bare names; blank entries are skipped, other relative ones are logged and skipped.</param>
    /// <param name="isWindows">Selects the case and separator rules; injectable so every OS's rules can be tested anywhere.</param>
    public ScanExcludeMatcher(IEnumerable<string> patterns, bool isWindows)
    {
        _isWindows = isWindows;
        var list = new List<string[]>();
        var bare = new List<string>();
        foreach (var raw in patterns ?? Array.Empty<string>())
        {
            var pattern = raw?.Trim();
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            if (IsBareName(pattern, isWindows))
            {
                bare.Add(pattern);
                continue;
            }

            if (!IsAbsolute(pattern, isWindows))
            {
                Log.Warning("Scan exclude path ignored, it is not an absolute path or a bare folder name: {Pattern}", pattern);
                continue;
            }

            var segments = Split(pattern, isWindows);
            if (segments.Length > 0)
            {
                list.Add(segments);
            }
        }

        _patterns = list.ToArray();
        _bareNames = bare.ToArray();

        foreach (var pattern in NeverMatchingPatterns(patterns, isWindows))
        {
            Log.Warning("Scan exclude path can never match on this OS, it is kept but has no effect: {Pattern}", pattern);
        }
    }

    /// <summary>
    /// Non-blank patterns written for another OS's paths, so they can never match here: a rooted "/..." path on
    /// Windows (a "//" UNC path is fine), or a "C:\..." drive or "\\" UNC path on Linux and macOS.
    /// </summary>
    public static IReadOnlyList<string> NeverMatchingPatterns(IEnumerable<string> patterns, bool isWindows)
    {
        var never = new List<string>();
        foreach (var raw in patterns ?? Array.Empty<string>())
        {
            var pattern = raw?.Trim();
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            var unixRooted = pattern[0] == '/' && !pattern.StartsWith("//", StringComparison.Ordinal);
            var windowsStyle = (pattern.Length >= 3 && char.IsAsciiLetter(pattern[0]) && pattern[1] == ':' && (pattern[2] == '\\' || pattern[2] == '/'))
                               || pattern.StartsWith(@"\\", StringComparison.Ordinal);
            if (isWindows ? unixRooted : windowsStyle)
            {
                never.Add(pattern);
            }
        }

        return never;
    }

    /// <summary>
    /// The non-blank patterns that are neither an absolute path nor a bare folder name, so the matcher ignores them.
    /// </summary>
    public static IReadOnlyList<string> InvalidPatterns(IEnumerable<string> patterns, bool isWindows)
    {
        var invalid = new List<string>();
        foreach (var raw in patterns ?? Array.Empty<string>())
        {
            var pattern = raw?.Trim();
            if (!string.IsNullOrEmpty(pattern) && !IsBareName(pattern, isWindows) && !IsAbsolute(pattern, isWindows))
            {
                invalid.Add(pattern);
            }
        }

        return invalid;
    }

    /// <summary>Linux-only built-in excludes: pseudo and volatile folders that are never worth cataloguing.</summary>
    private static readonly string[] LinuxDefaultExcludes =
    [
        "/proc", "/sys", "/dev", "/run", "/tmp", "/var/tmp", "/var/cache", "/var/spool", "/var/lock", "/lost+found", "/home/*/.cache"
    ];

    /// <summary>
    /// macOS-only built-in excludes: the device tree, and the per-volume Spotlight index and FSEvents store at a volume root.
    /// /dev is in Apple's File System Programming Guide; the two dot folders are described in Eclectic Light's APFS notes.
    /// The swap/VM paths (/private/var/vm, /System/Volumes/VM) are left out: only forum posts confirm them.
    /// </summary>
    private static readonly string[] MacOsDefaultExcludes = ["/dev", "/.Spotlight-V100", "/.fseventsd"];

    /// <summary>
    /// User patterns plus, on Linux or macOS with <paramref name="useDefaultExcludes"/>, that OS's built-in list.
    /// The OS is injected so the on/off/additive rules can be tested on any host.
    /// </summary>
    public static ScanExcludeMatcher Create(IEnumerable<string> userPatterns, bool useDefaultExcludes,
        bool isWindows, bool isLinux, bool isMacOs = false)
    {
        var all = new List<string>(userPatterns ?? Array.Empty<string>());
        if (useDefaultExcludes && isLinux)
        {
            all.AddRange(LinuxDefaultExcludes);
        }
        else if (useDefaultExcludes && isMacOs)
        {
            all.AddRange(MacOsDefaultExcludes);
        }

        return new ScanExcludeMatcher(all, isWindows);
    }

    /// <summary>
    /// The matcher for the running OS from the Scan config; a null section means no user excludes, defaults on.
    /// <paramref name="extraPatterns"/> (the per-run <c>--exclude</c> option) add to the configured patterns.
    /// </summary>
    public static ScanExcludeMatcher ForScanConfig(ScanSection scan, IEnumerable<string> extraPatterns = null)
    {
        var user = extraPatterns == null
            ? scan?.ExcludePaths
            : (scan?.ExcludePaths ?? Enumerable.Empty<string>()).Concat(extraPatterns);
        return Create(user, scan?.UseDefaultExcludes ?? true,
            OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS());
    }

    /// <summary>
    /// The matcher to use for one scan: patterns that match the scan root or an ancestor of it are dropped,
    /// because the explicitly named root is the user's intent. Patterns strictly below the root still apply.
    /// The root is normalised first (full path, trailing separator trimmed except for a volume root), so a relative
    /// path or one containing ".." cannot defeat the ancestor check.
    /// Bare names are only checked below the root, so a root with that name is still scanned.
    /// Done once per scan so the per-directory check stays allocation-free.
    /// </summary>
    public ScanExcludeMatcher ForScanRoot(string scanRoot)
    {
        if (IsEmpty || string.IsNullOrEmpty(scanRoot))
        {
            return this;
        }

        // Paths reaching IsExcluded start with the root exactly as the scan was given it, so skip that many chars.
        var bareStart = scanRoot.Length;
        while (bareStart > 0 && IsSeparator(scanRoot[bareStart - 1], _isWindows))
        {
            bareStart--;
        }

        // Host path APIs only apply when this matcher follows the host's rules (always true outside tests).
        if (_isWindows == OperatingSystem.IsWindows())
        {
            scanRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scanRoot));
        }

        var kept = new List<string[]>(_patterns.Length);
        foreach (var pattern in _patterns)
        {
            if (!MatchesPrefix(scanRoot.AsSpan(), pattern))
            {
                kept.Add(pattern);
            }
        }

        if (kept.Count == _patterns.Length && _bareNames.Length == 0)
        {
            return this; // nothing dropped and no bare names need the root offset
        }

        return new ScanExcludeMatcher(kept.ToArray(), _bareNames, bareStart, _isWindows);
    }

    public bool IsEmpty => _patterns.Length == 0 && _bareNames.Length == 0;

    /// <summary>True when <paramref name="fullPath"/> is, or is under, an excluded folder.</summary>
    public bool IsExcluded(string fullPath)
    {
        if (IsEmpty || string.IsNullOrEmpty(fullPath))
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

        return _bareNames.Length > 0 && MatchesAnyBareName(fullPath.AsSpan());
    }

    // True when the path's own (last) segment, if below the scan root, matches a bare name. An excluded parent is
    // never descended into, so ancestors need no check; a path that is the root itself has no segment below it.
    private bool MatchesAnyBareName(ReadOnlySpan<char> path)
    {
        var end = path.Length;
        while (end > 0 && IsSeparator(path[end - 1], _isWindows))
        {
            end--;
        }

        var start = end;
        while (start > 0 && !IsSeparator(path[start - 1], _isWindows))
        {
            start--;
        }

        if (end <= start || start < _bareStart)
        {
            return false;
        }

        var segment = path.Slice(start, end - start);
        foreach (var name in _bareNames)
        {
            if (SegmentMatches(segment, name))
            {
                return true;
            }
        }

        return false;
    }

    // A bare name has no path separator; on Linux a backslash is an ordinary name character.
    private static bool IsBareName(string p, bool isWindows)
    {
        foreach (var c in p)
        {
            if (IsSeparator(c, isWindows))
            {
                return false;
            }
        }

        // Not a folder name: "." and ".." are navigation, and an all-'*' name would exclude everything.
        return !IsAbsolute(p, isWindows) && p != "." && p != ".." && p.AsSpan().Trim('*').Length > 0;
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
