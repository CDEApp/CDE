using System;
using System.Collections.Generic;

namespace cdeLib.Entities;

/// <summary>
/// Scan-local string pool, looked up by <see cref="ReadOnlySpan{T}"/> so a repeated name allocates
/// nothing. Replaces <see cref="string.Intern(string)"/> during a scan: the strings are shared within
/// the scan, nothing is added to the process-wide intern table, and the pool itself is freed when the
/// scan ends. Thread safe: a parallel scan (#235) shares one pool, split into locked stripes by hash so
/// workers rarely wait on each other and every name is still stored once.
/// </summary>
internal sealed class ScanNamePool
{
    private const int StripeCount = 32;
    private readonly HashSet<string>[] _sets = new HashSet<string>[StripeCount];
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[] _lookups =
        new HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[StripeCount];

    public ScanNamePool()
    {
        for (var i = 0; i < StripeCount; i++)
        {
            _sets[i] = new HashSet<string>(StringComparer.Ordinal);
            _lookups[i] = _sets[i].GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }

    public int Count
    {
        get
        {
            var n = 0;
            foreach (var set in _sets)
            {
                lock (set) n += set.Count;
            }
            return n;
        }
    }

    public string Get(ReadOnlySpan<char> value)
    {
        var stripe = (int)((uint)string.GetHashCode(value) % StripeCount);
        var set = _sets[stripe];
        lock (set)
        {
            if (_lookups[stripe].TryGetValue(value, out var existing))
            {
                return existing;
            }

            var s = value.ToString();
            set.Add(s);
            return s;
        }
    }
}
