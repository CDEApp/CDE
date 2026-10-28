using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Superpower;
using Superpower.Parsers;

namespace cdeLib;

/// <summary>
/// The <c>--template</c> text for a find/grep match line, parsed once into literal and placeholder segments so
/// rendering a match does no parsing. Placeholders (case-insensitive): <c>{catalog}</c>, <c>{fullpath}</c>,
/// <c>{filename}</c>, <c>{directory}</c>, <c>{size}</c> and <c>{modified}</c>. <c>{modified}</c> and <c>{size}</c>
/// take an optional format after a colon, such as <c>{modified:yyyy-MM-dd}</c> or <c>{size:MB}</c>. <c>{{</c> and <c>}}</c> are literal braces;
/// <c>\t</c> and <c>\n</c> are a tab and a newline; any other backslash is literal so Windows paths can be
/// written in a template.
/// </summary>
public sealed class MatchTemplate
{
    private enum Part { Literal, Catalog, FullPath, FileName, Directory, Size, Modified }

    // The tokens of a {modified:...} format. They are case-sensitive: MM is the month, mm the minutes.
    private enum DateToken { Literal, Year, Month, Day, Hour, Minute, Second }

    // The formats of {size}. The values from Bytes up are the power of 1024 the size is divided by.
    private enum SizeUnit { Auto = -1, Bytes = 0, KB = 1, MB = 2, GB = 3, TB = 4 }

    private static readonly string[] AutoSuffix = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    private const string ValidNames = "{catalog}, {fullpath}, {filename}, {directory}, {size}, {modified}";
    private const string ValidSizeFormats = "auto, B, KB, MB, GB, TB";
    private const string DefaultModifiedFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Segment[] _segments;

    [ThreadStatic] private static StringBuilder _builder;

    private readonly record struct Segment(
        Part Part, string Text, (DateToken Token, string Text)[] Date, SizeUnit Unit = SizeUnit.Auto);

    private MatchTemplate(Segment[] segments) => _segments = segments;

    // ---- grammar ----
    // A template is a run of literal text, escapes and {name} / {name:format} placeholders. A placeholder's name
    // and format are read as plain text here and checked afterwards (ToSegment), which keeps the error messages
    // for an unknown name or a bad format specific.

    private static Segment Literal(string text) => new(Part.Literal, text, null);

    private static readonly TextParser<Segment> PlainText =
        Character.Matching(c => c is not ('{' or '}' or '\\'), "text").AtLeastOnce()
            .Select(chars => Literal(new string(chars)));

    // {{ and }} are braces, \t and \n a tab and a newline. Any other backslash is a plain backslash.
    private static readonly TextParser<Segment> EscapedText =
        Span.EqualTo("{{").Value(Literal("{")).Try()
            .Or(Span.EqualTo("}}").Value(Literal("}")).Try())
            .Or(Span.EqualTo("\\t").Value(Literal("\t")).Try())
            .Or(Span.EqualTo("\\n").Value(Literal("\n")).Try())
            .Or(Character.EqualTo('\\').Value(Literal("\\")));

    private static readonly TextParser<Segment> Placeholder =
        from open in Character.EqualTo('{')
        from name in Character.Matching(c => c is not (':' or '}'), "placeholder name").Many()
        from format in Character.EqualTo(':')
            .IgnoreThen(Character.Matching(c => c != '}', "format").Many())
            .OptionalOrDefault()
        from close in Character.EqualTo('}')
        select ToSegment(new string(name), format == null ? null : new string(format));

    private static readonly TextParser<Segment[]> TemplateGrammar =
        EscapedText.Or(PlainText).Or(Placeholder).Many().AtEnd();

    // A {modified:...} format is a run of date tokens, longest first, and anything else is literal.
    private static TextParser<(DateToken Token, string Text)> DateTokenText(string text, DateToken token) =>
        Span.EqualTo(text).Value((token, (string)null)).Try();

    private static readonly TextParser<(DateToken Token, string Text)> DateItem =
        DateTokenText("yyyy", DateToken.Year)
            .Or(DateTokenText("MM", DateToken.Month))
            .Or(DateTokenText("dd", DateToken.Day))
            .Or(DateTokenText("HH", DateToken.Hour))
            .Or(DateTokenText("mm", DateToken.Minute))
            .Or(DateTokenText("ss", DateToken.Second))
            .Or(Character.AnyChar.Select(c => (DateToken.Literal, c.ToString())));

    private static readonly TextParser<(DateToken Token, string Text)[]> DateGrammar = DateItem.Many().AtEnd();

    /// <summary>Parse a template. Throws <see cref="FormatException"/> for an unknown placeholder, a bad format or unbalanced braces.</summary>
    public static MatchTemplate Parse(string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);

        try
        {
            return new MatchTemplate(MergeLiterals(TemplateGrammar.Parse(template)));
        }
        catch (ParseException ex)
        {
            throw new FormatException($"Invalid template: {ex.Message}", ex);
        }
    }

    private static Segment ToSegment(string name, string format)
    {
        var part = name.ToLowerInvariant() switch
        {
            "catalog" => Part.Catalog,
            "fullpath" => Part.FullPath,
            "filename" => Part.FileName,
            "directory" => Part.Directory,
            "size" => Part.Size,
            "modified" => Part.Modified,
            _ => throw new FormatException(
                $"Unknown template placeholder '{{{name}}}'. Valid placeholders are {ValidNames}."),
        };

        if (part == Part.Modified)
        {
            if (format is { Length: 0 })
                throw new FormatException("Template placeholder '{modified:}' has an empty format.");
            return new Segment(part, null, MergeLiterals(DateGrammar.Parse(format ?? DefaultModifiedFormat)));
        }

        if (part == Part.Size)
            return new Segment(part, null, null, ParseSizeUnit(format));

        if (format != null)
            throw new FormatException(
                $"Template placeholder '{{{name}}}' does not take a format; only {{modified}} and {{size}} do.");
        return new Segment(part, null, null);
    }

    private static SizeUnit ParseSizeUnit(string format) => format?.ToLowerInvariant() switch
    {
        null or "auto" => SizeUnit.Auto,
        "b" => SizeUnit.Bytes,
        "kb" => SizeUnit.KB,
        "mb" => SizeUnit.MB,
        "gb" => SizeUnit.GB,
        "tb" => SizeUnit.TB,
        _ => throw new FormatException(
            $"Unknown size format '{format}' in '{{size:{format}}}'. Valid size formats are {ValidSizeFormats}."),
    };

    // Joins neighbouring literals so a match is rendered with as few appends as possible.
    private static Segment[] MergeLiterals(Segment[] segments)
    {
        var merged = new List<Segment>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment.Part == Part.Literal && merged.Count > 0 && merged[^1].Part == Part.Literal)
                merged[^1] = Literal(merged[^1].Text + segment.Text);
            else
                merged.Add(segment);
        }

        return [.. merged];
    }

    private static (DateToken, string)[] MergeLiterals((DateToken Token, string Text)[] tokens)
    {
        var merged = new List<(DateToken Token, string Text)>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.Token == DateToken.Literal && merged.Count > 0 && merged[^1].Token == DateToken.Literal)
                merged[^1] = (DateToken.Literal, merged[^1].Text + token.Text);
            else
                merged.Add(token);
        }

        return [.. merged];
    }

    /// <summary>
    /// Render one match. <paramref name="catalog"/> is the catalog's file name or path (only the file name,
    /// without its extension, is used); <paramref name="fullPath"/> is the matched entry's full path. Paths are
    /// split on the last <c>\</c> or <c>/</c> rather than with <see cref="System.IO.Path"/>, so a catalog scanned
    /// on another OS splits correctly. <paramref name="size"/> is written in bytes and
    /// <paramref name="modified"/> in whatever time zone it carries.
    /// </summary>
    public string Render(string catalog, string fullPath, long size = 0, DateTime modified = default)
    {
        var sb = _builder ??= new StringBuilder();
        sb.Clear();

        fullPath ??= string.Empty;
        var slash = fullPath.AsSpan().LastIndexOfAny('\\', '/');

        foreach (var segment in _segments)
        {
            switch (segment.Part)
            {
                case Part.Literal:
                    sb.Append(segment.Text);
                    break;
                case Part.Catalog:
                    sb.Append(CatalogName(catalog));
                    break;
                case Part.FullPath:
                    sb.Append(fullPath);
                    break;
                case Part.FileName:
                    sb.Append(fullPath.AsSpan(slash + 1));
                    break;
                case Part.Directory:
                    sb.Append(fullPath.AsSpan(0, slash < 0 ? 0 : slash));
                    break;
                case Part.Size:
                    AppendSize(sb, size, segment.Unit);
                    break;
                case Part.Modified:
                    AppendDate(sb, segment.Date, modified);
                    break;
            }
        }

        return sb.ToString();
    }

    // Sizes use powers of 1024 with one decimal, rounded half away from zero and written culture-invariantly,
    // as cdeAppCore's SizeFormatter does for the GUIs (cdeLib cannot reference it; a cdeAppCoreTest runs the
    // shared cases through both). Every format writes the unit name after the value, such as "4096 KB"; auto
    // picks the unit, and writes a size of 0 as just "0" like the GUIs.
    private static void AppendSize(StringBuilder sb, long size, SizeUnit unit)
    {
        if (unit == SizeUnit.Auto)
        {
            if (size < 0)
                sb.Append(CultureInfo.InvariantCulture, $"{size} B");
            else
                AppendAutoSize(sb, size);
        }
        else if (unit == SizeUnit.Bytes || size < 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{size} B");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"{ScaledSize(size, (int)unit)} {AutoSuffix[(int)unit]}");
        }
    }

    private static void AppendAutoSize(StringBuilder sb, long size)
    {
        if (size == 0)
        {
            sb.Append('0');
            return;
        }

        var place = (63 - System.Numerics.BitOperations.LeadingZeroCount((ulong)size)) / 10; // floor(log1024(size))
        var scaled = ScaledSize(size, place);
        if (scaled >= 1024 && place < AutoSuffix.Length - 1)
        {
            // 1023.96 KB rounds to 1024; show it as 1 MB instead.
            place++;
            scaled = ScaledSize(size, place);
        }

        sb.Append(CultureInfo.InvariantCulture, $"{scaled} {AutoSuffix[place]}");
    }

    private static double ScaledSize(long size, int place) =>
        Math.Round(size / Math.Pow(1024, place), 1, MidpointRounding.AwayFromZero);

    private static void AppendDate(StringBuilder sb, (DateToken Token, string Text)[] tokens, DateTime date)
    {
        foreach (var (token, text) in tokens)
        {
            switch (token)
            {
                case DateToken.Literal: sb.Append(text); break;
                case DateToken.Year: AppendPadded(sb, date.Year, 4); break;
                case DateToken.Month: AppendPadded(sb, date.Month, 2); break;
                case DateToken.Day: AppendPadded(sb, date.Day, 2); break;
                case DateToken.Hour: AppendPadded(sb, date.Hour, 2); break;
                case DateToken.Minute: AppendPadded(sb, date.Minute, 2); break;
                case DateToken.Second: AppendPadded(sb, date.Second, 2); break;
            }
        }
    }

    // Appends a non-negative value as digits, zero-padded to the width, without allocating a string.
    private static void AppendPadded(StringBuilder sb, int value, int width)
    {
        for (var divisor = width == 4 ? 1000 : 10; divisor > 0; divisor /= 10)
            sb.Append((char)('0' + (value / divisor % 10)));
    }

    private static ReadOnlySpan<char> CatalogName(string catalog)
    {
        if (string.IsNullOrEmpty(catalog)) return default;
        var span = catalog.AsSpan();
        var name = span[(span.LastIndexOfAny('\\', '/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}
