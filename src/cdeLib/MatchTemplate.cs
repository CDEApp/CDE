using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace cdeLib;

/// <summary>
/// The <c>--template</c> text for a find/grep match line, parsed once into literal and placeholder segments so
/// rendering a match does no parsing. Placeholders (case-insensitive): <c>{catalog}</c>, <c>{fullpath}</c>,
/// <c>{filename}</c>, <c>{directory}</c>, <c>{size}</c> and <c>{modified}</c>. <c>{modified}</c> takes an optional
/// format after a colon, such as <c>{modified:yyyy-MM-dd}</c>. <c>{{</c> and <c>}}</c> are literal braces;
/// <c>\t</c> and <c>\n</c> are a tab and a newline; any other backslash is literal so Windows paths can be
/// written in a template.
/// </summary>
public sealed class MatchTemplate
{
    private enum Part { Literal, Catalog, FullPath, FileName, Directory, Size, Modified }

    // The tokens of a {modified:...} format. They are case-sensitive: MM is the month, mm the minutes.
    private enum DateToken { Literal, Year, Month, Day, Hour, Minute, Second }

    private const string ValidNames = "{catalog}, {fullpath}, {filename}, {directory}, {size}, {modified}";
    private const string DefaultModifiedFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Segment[] _segments;

    [ThreadStatic] private static StringBuilder _builder;

    private readonly record struct Segment(Part Part, string Text, (DateToken Token, string Text)[] Date);

    private MatchTemplate(Segment[] segments) => _segments = segments;

    /// <summary>Parse a template. Throws <see cref="FormatException"/> for an unknown placeholder, a bad format or unbalanced braces.</summary>
    public static MatchTemplate Parse(string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);

        var segments = new List<Segment>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            segments.Add(new Segment(Part.Literal, literal.ToString(), null));
            literal.Clear();
        }

        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            var next = i + 1 < template.Length ? template[i + 1] : '\0';
            switch (c)
            {
                case '{' when next == '{':
                    literal.Append('{');
                    i++;
                    break;
                case '}' when next == '}':
                    literal.Append('}');
                    i++;
                    break;
                case '{':
                    var end = template.IndexOf('}', i + 1);
                    if (end < 0) throw new FormatException($"Template has an unclosed '{{' at position {i}.");
                    FlushLiteral();
                    segments.Add(ParsePlaceholder(template.Substring(i + 1, end - i - 1)));
                    i = end;
                    break;
                case '}':
                    throw new FormatException($"Template has an unmatched '}}' at position {i}; use '}}}}' for a literal brace.");
                case '\\' when next == 't':
                    literal.Append('\t');
                    i++;
                    break;
                case '\\' when next == 'n':
                    literal.Append('\n');
                    i++;
                    break;
                default:
                    literal.Append(c);
                    break;
            }
        }

        FlushLiteral();
        return new MatchTemplate([.. segments]);
    }

    private static Segment ParsePlaceholder(string body)
    {
        var colon = body.IndexOf(':');
        var name = colon < 0 ? body : body[..colon];
        var format = colon < 0 ? null : body[(colon + 1)..];

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
            return new Segment(part, null, ParseDateFormat(format ?? DefaultModifiedFormat));
        }

        if (format != null)
            throw new FormatException($"Template placeholder '{{{name}}}' does not take a format; only {{modified}} does.");
        return new Segment(part, null, null);
    }

    private static (DateToken, string)[] ParseDateFormat(string format)
    {
        var tokens = new List<(DateToken, string)>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            tokens.Add((DateToken.Literal, literal.ToString()));
            literal.Clear();
        }

        for (var i = 0; i < format.Length; i++)
        {
            var token = TokenAt(format, i);
            if (token == DateToken.Literal)
            {
                literal.Append(format[i]);
                continue;
            }

            FlushLiteral();
            tokens.Add((token, null));
            i += TokenLength(token) - 1;
        }

        FlushLiteral();
        return [.. tokens];
    }

    private static DateToken TokenAt(string format, int i)
    {
        if (StartsWith(format, i, "yyyy")) return DateToken.Year;
        if (StartsWith(format, i, "MM")) return DateToken.Month;
        if (StartsWith(format, i, "dd")) return DateToken.Day;
        if (StartsWith(format, i, "HH")) return DateToken.Hour;
        if (StartsWith(format, i, "mm")) return DateToken.Minute;
        if (StartsWith(format, i, "ss")) return DateToken.Second;
        return DateToken.Literal;
    }

    private static bool StartsWith(string s, int i, string token) =>
        string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

    private static int TokenLength(DateToken token) => token == DateToken.Year ? 4 : 2;

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
                    sb.Append(CultureInfo.InvariantCulture, $"{size}");
                    break;
                case Part.Modified:
                    AppendDate(sb, segment.Date, modified);
                    break;
            }
        }

        return sb.ToString();
    }

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
