using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace cdeLib;

/// <summary>
/// The value of an Everything numeric function (<c>size:</c>, <c>date-modified:</c>), parsed once into the one shape
/// every form reduces to: a half-open interval <c>[lo, hi)</c> that either must contain the value or must not.
/// Forms: a bare value (equal within its granularity, <c>size:1kb</c> is 1024..2047), <c>=</c>/<c>==</c> (exact),
/// <c>!=</c> (not exact), <c>!</c> (not within the granularity), <c>&lt; &lt;= &gt; &gt;=</c>, and the ranges
/// <c>a..b</c> (sizes also <c>a-b</c>). Sizes compare against the exact value. Dates compare against the whole
/// granule, so <c>&gt;2026-07-02</c> is after that whole day and <c>&lt;=2026-07-02</c> includes it. A relative date
/// (<c>10mins</c>) is the instant that long ago; bare it means "since then", <c>&gt;</c> is newer and <c>&lt;</c> is older.
/// Ranges include the end value (for dates its whole granule).
/// </summary>
internal sealed partial class NumericCriterion
{
    private const long MaxSize = 1L << 62;

    private readonly long _lo;
    private readonly long _hi; // exclusive
    private readonly bool _negate;

    private NumericCriterion(long lo, long hi, bool negate)
    {
        _lo = lo;
        _hi = hi;
        _negate = negate;
    }

    public bool Matches(long value) => (value >= _lo && value < _hi) != _negate;

    /// <summary>One parsed value: the interval it stands for and whether it is a relative date.</summary>
    private readonly record struct Value(long Lo, long Hi, bool Relative);

    /// <summary>Parse a <c>size:</c> value. Throws <see cref="FormatException"/> with a message fit for the user.</summary>
    public static NumericCriterion ParseSize(string text) =>
        Parse(text, ParseSizeValue, spans: false, dashRange: true);

    /// <summary>Parse a <c>date-modified:</c> value. <paramref name="now"/> is the local wall-clock time.</summary>
    public static NumericCriterion ParseDate(string text, DateTime now) =>
        Parse(text, v => ParseDateValue(v, now), spans: true, dashRange: false);

    private static NumericCriterion Parse(string text, Func<string, Value> parseValue, bool spans, bool dashRange)
    {
        text = text.Trim();
        if (text.Length == 0) throw new FormatException("A value is missing");
        if (text.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("'unknown' is not supported");

        var op = Operators.Match(text) is { Success: true } m ? m.Value : string.Empty;
        var rest = text[op.Length..];

        if (op.Length == 0 && SplitRange(rest, dashRange) is var (left, right))
        {
            var a = parseValue(left);
            var b = parseValue(right);
            if (b.Lo < a.Lo) (a, b) = (b, a);
            return new NumericCriterion(a.Lo, spans || a.Relative ? b.Hi : b.Lo + 1, negate: false);
        }

        var v = parseValue(rest);
        return op switch
        {
            "" when v.Relative => new NumericCriterion(v.Lo, long.MaxValue, false),
            "" => new NumericCriterion(v.Lo, v.Hi, false),
            "!" => new NumericCriterion(v.Lo, v.Hi, true),
            "=" or "==" => new NumericCriterion(v.Lo, v.Lo + 1, false),
            "!=" => new NumericCriterion(v.Lo, v.Lo + 1, true),
            "<" => new NumericCriterion(long.MinValue, v.Lo, false),
            "<=" => new NumericCriterion(long.MinValue, spans ? v.Hi : v.Lo + 1, false),
            ">" => new NumericCriterion(spans ? v.Hi : v.Lo + 1, long.MaxValue, false),
            _ => new NumericCriterion(v.Lo, long.MaxValue, false), // ">="
        };
    }

    [GeneratedRegex(@"^(<=|>=|==|!=|<|>|=|!)")]
    private static partial Regex Operators { get; }

    private static (string Left, string Right)? SplitRange(string text, bool dashRange)
    {
        var dots = text.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0) return (text[..dots], text[(dots + 2)..]);
        if (!dashRange) return null;
        var dash = text.IndexOf('-');
        return dash > 0 && text.IndexOf('-', dash + 1) < 0 ? (text[..dash], text[(dash + 1)..]) : null;
    }

    // ---- sizes ----

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*([a-z]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex SizeText { get; }

    private static Value ParseSizeValue(string text)
    {
        var m = SizeText.Match(text.Trim());
        if (!m.Success) throw new FormatException($"'{text}' is not a size; use a number with an optional unit such as 10mb");

        var unit = UnitBytes(m.Groups[2].Value.ToLowerInvariant())
                   ?? throw new FormatException($"'{m.Groups[2].Value}' is not a size unit; use b, kb, mb, gb or tb");
        var number = m.Groups[1].Value;
        if (number.Length > 18) throw new FormatException($"'{text}' is too large");

        var amount = decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var bytes = amount * unit;
        if (bytes > MaxSize) throw new FormatException($"'{text}' is too large");

        var fractionDigits = number.Contains('.') ? number.Length - number.IndexOf('.') - 1 : 0;
        var granule = unit;
        for (var i = 0; i < fractionDigits && granule > 1; i++) granule /= 10;
        var lo = (long)bytes;
        return new Value(lo, lo + Math.Max(1, granule), Relative: false);
    }

    private static long? UnitBytes(string unit) => unit switch
    {
        "" or "b" => 1L,
        "k" or "kb" or "kib" => 1L << 10,
        "m" or "mb" or "mib" => 1L << 20,
        "g" or "gb" or "gib" => 1L << 30,
        "t" or "tb" or "tib" => 1L << 40,
        _ => null,
    };

    // ---- dates ----

    [GeneratedRegex(@"^(\d+)\s*(sec|secs|second|seconds|min|mins|minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeText { get; }

    [GeneratedRegex(@"^(\d{4})(?:-(\d{2})(?:-(\d{2})(?:T(\d{2})(?::(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?)?)?)?)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IsoText { get; }

    private static Value ParseDateValue(string text, DateTime now)
    {
        text = text.Trim();
        switch (text.ToLowerInvariant())
        {
            case "today": return Day(now.Date);
            case "yesterday": return Day(now.Date.AddDays(-1));
            case "tomorrow": return Day(now.Date.AddDays(1));
        }

        var relative = RelativeText.Match(text);
        if (relative.Success) return Ago(relative, now);

        var iso = IsoText.Match(text);
        if (iso.Success)
        {
            try { return Iso(iso); }
            catch (ArgumentOutOfRangeException) { throw new FormatException($"'{text}' is not a valid date"); }
        }

        throw new FormatException(
            $"'{text}' is not a date; use today, yesterday, a span such as 3days, or an ISO date such as 2026-07-02");
    }

    private static Value Day(DateTime start) => new(start.Ticks, start.AddDays(1).Ticks, Relative: false);

    private static Value Ago(Match m, DateTime now)
    {
        if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            throw new FormatException($"'{m.Value}' is too large");
        var unit = m.Groups[2].Value.ToLowerInvariant();
        try
        {
            var then = unit switch
            {
                "sec" or "secs" or "second" or "seconds" => now.AddSeconds(-n),
                "min" or "mins" or "minute" or "minutes" => now.AddMinutes(-n),
                "hour" or "hours" => now.AddHours(-n),
                "day" or "days" => now.AddDays(-n),
                "week" or "weeks" => now.AddDays(-7.0 * n),
                "month" or "months" => now.AddMonths(-n),
                _ => now.AddYears(-n),
            };
            return new Value(then.Ticks, then.Ticks + 1, Relative: true);
        }
        catch (ArgumentOutOfRangeException) { throw new FormatException($"'{m.Value}' is too far back"); }
    }

    private static Value Iso(Match m)
    {
        int Part(int group, int fallback) => m.Groups[group].Success
            ? int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture)
            : fallback;

        var year = Part(1, 1);
        var start = new DateTime(
            year, Part(2, 1), Part(3, 1), Part(4, 0), Part(5, 0), Part(6, 0), DateTimeKind.Unspecified);
        if (m.Groups[7].Success)
        {
            var fraction = m.Groups[7].Value.PadRight(3, '0');
            start = start.AddMilliseconds(int.Parse(fraction, CultureInfo.InvariantCulture));
        }

        DateTime end;
        try
        {
            end = m.Groups[7].Success ? start.AddMilliseconds(1)
                : m.Groups[6].Success ? start.AddSeconds(1)
                : m.Groups[5].Success ? start.AddMinutes(1)
                : m.Groups[4].Success ? start.AddHours(1)
                : m.Groups[3].Success ? start.AddDays(1)
                : m.Groups[2].Success ? start.AddMonths(1)
                : start.AddYears(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            end = DateTime.MaxValue;
        }

        return new Value(start.Ticks, end.Ticks, Relative: false);
    }
}
