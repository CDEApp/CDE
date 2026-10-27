using System;
using System.Collections.Generic;

namespace cdeLib.Entities;

/// <summary>
/// Scan-local string pool, looked up by <see cref="ReadOnlySpan{T}"/> so a repeated name allocates
/// nothing. Replaces <see cref="string.Intern(string)"/> during a scan: the strings are shared within
/// the scan, nothing is added to the process-wide intern table, and the pool itself is freed when the
/// scan ends. Not thread safe (a scan is single threaded).
/// </summary>
internal sealed class ScanNamePool
{
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public ScanNamePool()
    {
        _lookup = _set.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public int Count => _set.Count;

    public string Get(ReadOnlySpan<char> value)
    {
        if (_lookup.TryGetValue(value, out var existing))
        {
            return existing;
        }

        var s = value.ToString();
        _set.Add(s);
        return s;
    }
}
