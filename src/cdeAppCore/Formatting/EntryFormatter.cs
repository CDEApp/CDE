using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeAppCore.Formatting;

/// <summary>
/// Pure size/date cell formatting for catalog rows, independent of any UI framework. Holds the
/// configured date-format string plus a bounded date cache (the same amortisation the WinForms
/// presenter did inline). Cell strings are produced identically to the previous WinForms rendering.
/// </summary>
public sealed class EntryFormatter
{
    private readonly string _dateFormat;

    // Cache for formatted date strings (bounded, matches the prior presenter behaviour).
    private readonly Dictionary<DateTime, string> _dateCache = new(1024);

    public EntryFormatter(string dateFormat)
    {
        _dateFormat = dateFormat;
    }

    /// <summary>Format a date with the configured format string, caching up to 10,000 distinct values.</summary>
    public string FormatDate(DateTime date)
    {
        if (_dateCache.TryGetValue(date, out var cached))
            return cached;

        var result = string.Format(_dateFormat, date);

        if (_dateCache.Count < 10000)
            _dateCache[date] = result;

        return result;
    }

    /// <summary>The Modified column cell: <c>&lt;Bad Date&gt;</c> for bad dates, otherwise the formatted date.</summary>
    public string FormatModifiedCell(ICommonEntry entry)
        => entry.IsModifiedBad ? "<Bad Date>" : FormatDate(entry.Modified);

    /// <summary>
    /// The Size column cell for a directory listing row: the raw size for files, or a human-readable
    /// size annotated with <c>&lt;Dir&gt;</c> (and <c>R</c> for a reparse point) for directories.
    /// </summary>
    public static string FormatDirectorySizeCell(ICommonEntry entry)
    {
        if (!entry.IsDirectory)
            return entry.Size.ToString();

        var val = entry.Size.ToHRString() + " <Dir";
        if (entry.IsReparsePoint)
            val += " R";
        return val + ">";
    }
}
