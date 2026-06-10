using System;
using System.Collections.Generic;
using System.Threading;

namespace cdeWin;

public static class StringExtension
{
    private static readonly string[] Suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" };

    // Cache for formatted size strings (key: size, value: formatted string)
    private static readonly Dictionary<long, string> SizeCache = new(1024);
    private static readonly Lock SizeCacheLock = new();

    public static string ToHRString(this long val)
    {
        if (val == 0) return "0";

        // Check cache first
        lock (SizeCacheLock)
        {
            if (SizeCache.TryGetValue(val, out var cached))
                return cached;
        }

        // Compute
        var place = Convert.ToInt32(Math.Floor(Math.Log(val, 1024)));
        var num = Math.Round(val / Math.Pow(1024, place), 1);
        var result = $"{num} {Suffix[place]}";

        // Cache (with simple size limit)
        lock (SizeCacheLock)
        {
            if (SizeCache.Count < 10000)
                SizeCache[val] = result;
        }

        return result;
    }

    public static void ClearSizeCache()
    {
        lock (SizeCacheLock)
        {
            SizeCache.Clear();
        }
    }
}