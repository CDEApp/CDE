using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace cdeAppCore.Formatting;

/// <summary>
/// Human-readable byte-size formatting (e.g. <c>1.5 MB</c>), independent of any UI framework.
/// Originally lifted from the WinForms <c>cdeWin.StringExtension</c>; now rounds half away from
/// zero and formats culture-invariantly so it matches cde-tauri. A small bounded cache amortises
/// the formatting of repeated sizes.
/// </summary>
public static class SizeFormatter
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
        // Half away from zero and culture-invariant, matching cde-tauri's toHRString
        // (cases shared via src/testdata/size-format-cases.json).
        var num = Math.Round(val / Math.Pow(1024, place), 1, MidpointRounding.AwayFromZero);
        if (num >= 1024 && place < Suffix.Length - 1)
        {
            // 1023.96 KB rounds to 1024; show it as 1 MB instead.
            place++;
            num = Math.Round(val / Math.Pow(1024, place), 1, MidpointRounding.AwayFromZero);
        }

        var result = string.Create(CultureInfo.InvariantCulture, $"{num} {Suffix[place]}");

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
