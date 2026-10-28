using System;
using System.Collections.Generic;
using System.Globalization;
using Superpower;
using Superpower.Parsers;

namespace cdeLib;

/// <summary>
/// The <c>--filter</c> options of a find/grep, parsed once so testing a match does no parsing. The only filter so
/// far is <c>{size:&lt;op&gt;&lt;value&gt;}</c>, such as <c>{size:&lt;4MB}</c> or <c>{size:&gt;=10KB}</c>. The
/// operators are <c>&lt; &lt;= &gt; &gt;= = !=</c>, and a value with no operator means equals. The value is a
/// number, with decimals if wanted, and an optional unit of B, KB, MB, GB or TB (1024 per step; no unit is bytes).
/// Several filters must all match.
/// </summary>
public sealed class MatchFilter
{
    // Equal must stay first: it is what a value with no operator parses to.
    private enum Comparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }

    private const string ValidFilters = "{size}";
    private const string ValidUnits = "B, KB, MB, GB, TB";

    private readonly SizeCondition[] _sizeConditions;

    private readonly record struct SizeCondition(Comparison Comparison, decimal Bytes);

    private MatchFilter(SizeCondition[] sizeConditions) => _sizeConditions = sizeConditions;

    // ---- grammar ----
    // A filter is {name:body}. The name and the body are read as plain text here and the body is then parsed
    // for that name (SizeGrammar), which keeps the error for an unknown filter specific.

    private static readonly TextParser<(string Name, string Body)> FilterGrammar =
        (from open in Character.EqualTo('{')
         from name in Character.Matching(c => c is not (':' or '}'), "filter name").Many()
         from colon in Character.EqualTo(':')
         from body in Character.Matching(c => c != '}', "filter value").Many()
         from close in Character.EqualTo('}')
         select (Name: new string(name), Body: new string(body))).AtEnd();

    private static readonly TextParser<Comparison> Operator =
        Span.EqualTo("<=").Value(Comparison.LessOrEqual).Try()
            .Or(Span.EqualTo(">=").Value(Comparison.GreaterOrEqual).Try())
            .Or(Span.EqualTo("!=").Value(Comparison.NotEqual).Try())
            .Or(Span.EqualTo("<").Value(Comparison.Less))
            .Or(Span.EqualTo(">").Value(Comparison.Greater))
            .Or(Span.EqualTo("=").Value(Comparison.Equal));

    // [operator] number [unit], with optional spaces around each part. The unit is checked afterwards.
    private static readonly TextParser<(Comparison Comparison, string Number, string Unit)> SizeGrammar =
        (from lead in Character.WhiteSpace.Many()
         from comparison in Operator.OptionalOrDefault()
         from afterOperator in Character.WhiteSpace.Many()
         from number in Numerics.Decimal
         from beforeUnit in Character.WhiteSpace.Many()
         from unit in Character.Letter.Many()
         from trail in Character.WhiteSpace.Many()
         select (comparison, number.ToStringValue(), new string(unit))).AtEnd();

    /// <summary>
    /// Parse one or more filters; a match must pass all of them. Throws <see cref="FormatException"/> for an unknown
    /// filter, a unit that is not known or text that is not a filter.
    /// </summary>
    public static MatchFilter Parse(IEnumerable<string> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var conditions = new List<SizeCondition>();
        foreach (var filter in filters)
            conditions.Add(ParseSizeFilter(filter ?? string.Empty));

        if (conditions.Count == 0)
            throw new ArgumentException("At least one filter is required.", nameof(filters));
        return new MatchFilter([.. conditions]);
    }

    private static SizeCondition ParseSizeFilter(string filter)
    {
        (string Name, string Body) parts;
        try
        {
            parts = FilterGrammar.Parse(filter);
        }
        catch (ParseException ex)
        {
            throw new FormatException($"Invalid filter '{filter}': {ex.Message}", ex);
        }

        if (!parts.Name.Equals("size", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Unknown filter '{{{parts.Name}}}'. Valid filters are {ValidFilters}.");

        (Comparison Comparison, string Number, string Unit) size;
        try
        {
            size = SizeGrammar.Parse(parts.Body);
        }
        catch (ParseException ex)
        {
            throw new FormatException(
                $"Invalid size filter '{filter}': {ex.Message} Use for example {{size:>10KB}} or {{size:<=4MB}}.", ex);
        }

        return new SizeCondition(size.Comparison, ToBytes(filter, size.Number, size.Unit));
    }

    private static decimal ToBytes(string filter, string number, string unit)
    {
        var multiplier = unit.ToLowerInvariant() switch
        {
            "" or "b" => 1m,
            "kb" => 1024m,
            "mb" => 1024m * 1024,
            "gb" => 1024m * 1024 * 1024,
            "tb" => 1024m * 1024 * 1024 * 1024,
            _ => throw new FormatException(
                $"Unknown size unit '{unit}' in filter '{filter}'. Valid units are {ValidUnits}."),
        };

        try
        {
            return decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) * multiplier;
        }
        catch (OverflowException ex)
        {
            throw new FormatException($"The size in filter '{filter}' is too large.", ex);
        }
    }

    /// <summary>True if a match of this size (in bytes) passes every filter.</summary>
    public bool Matches(long size)
    {
        foreach (var condition in _sizeConditions)
        {
            if (!Test(condition.Comparison, size, condition.Bytes)) return false;
        }

        return true;
    }

    private static bool Test(Comparison comparison, decimal value, decimal limit) => comparison switch
    {
        Comparison.Equal => value == limit,
        Comparison.NotEqual => value != limit,
        Comparison.Less => value < limit,
        Comparison.LessOrEqual => value <= limit,
        Comparison.Greater => value > limit,
        _ => value >= limit,
    };
}
