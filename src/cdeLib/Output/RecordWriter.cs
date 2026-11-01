using System;
using System.Buffers;
using System.Globalization;
using System.Linq;
using System.IO;

namespace cdeLib.Output;

/// <summary>How <c>find</c>, <c>dupes</c> and <c>hash</c> write their results (#173).</summary>
public enum OutputFormat
{
    /// <summary>The default console output, unchanged. No <see cref="IRecordWriter"/> is used.</summary>
    Text,

    /// <summary>One JSON object per line (JSON Lines).</summary>
    Json,

    /// <summary>RFC 4180 comma separated values with a header row.</summary>
    Csv,
}

/// <summary>
/// Writes a stream of records in a machine format, one record at a time so any size of result streams. A record is
/// <see cref="BeginRecord"/>, its fields in the order of the columns the writer was created with, then
/// <see cref="EndRecord"/>. The same field name and value go through the same escaping in every format and for every
/// command, and none of the field methods allocate. Lines end in <c>\n</c>. Not thread safe.
/// </summary>
public interface IRecordWriter
{
    void BeginRecord();

    /// <summary>A text field; may hold any characters.</summary>
    void Field(string name, ReadOnlySpan<char> value);

    void Field(string name, long value);

    void Field(string name, bool value);

    /// <summary>A time, written in UTC as <c>yyyy-MM-ddTHH:mm:ssZ</c>.</summary>
    void Field(string name, DateTime value);

    void EndRecord();

    void Flush();
}

/// <summary>Creates the <see cref="IRecordWriter"/> for a machine <see cref="OutputFormat"/>.</summary>
public static class RecordWriter
{
    /// <param name="format"><see cref="OutputFormat.Json"/> or <see cref="OutputFormat.Csv"/>.</param>
    /// <param name="output">Where the records go. Not owned: the caller flushes and disposes it.</param>
    /// <param name="columns">The field names, in order. CSV writes them as its header; JSON uses them as the keys.</param>
    public static IRecordWriter Create(OutputFormat format, TextWriter output, string[] columns)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(columns);
        return format switch
        {
            OutputFormat.Json => new JsonLinesWriter(output),
            OutputFormat.Csv => new CsvWriter(output, columns),
            _ => throw new ArgumentException($"{format} is not a machine format.", nameof(format)),
        };
    }

    internal const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    // Utc as is; Local converted; Unspecified is how the catalog stores a time with no kind, taken to be UTC.
    // Returns 0 for a time that is not a real modified time (default/zero ticks or before 1601, the Windows file time
    // epoch, which is what a catalog holds for a missing or bad date): the writers leave the field empty / null.
    internal static int FormatTime(DateTime value, Span<char> buffer)
    {
        if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
        if (value.Ticks == 0 || value.Year < 1601) return 0;
        return value.TryFormat(buffer, out var written, TimeFormat, CultureInfo.InvariantCulture) ? written : 0;
    }

    internal static int FormatLong(long value, Span<char> buffer) =>
        value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture) ? written : 0;
}

/// <summary>JSON Lines: <c>{"name":"value","size":1}</c> per record. Names are written as given, so they must be plain.</summary>
internal sealed class JsonLinesWriter(TextWriter output) : IRecordWriter
{
    // Quote, backslash, the C0 controls, DEL and the two line separators JavaScript treats as line ends.
    private static readonly SearchValues<char> NeedsEscape = SearchValues.Create(
        string.Concat(Enumerable.Range(0, 0x20).Concat([0x22, 0x5c, 0x7f, 0x2028, 0x2029]).Select(c => (char)c)));

    private bool _first;

    public void BeginRecord()
    {
        output.Write('{');
        _first = true;
    }

    public void Field(string name, ReadOnlySpan<char> value)
    {
        Key(name);
        output.Write('"');
        WriteEscaped(value);
        output.Write('"');
    }

    public void Field(string name, long value)
    {
        Key(name);
        Span<char> buffer = stackalloc char[24];
        output.Write(buffer[..RecordWriter.FormatLong(value, buffer)]);
    }

    public void Field(string name, bool value)
    {
        Key(name);
        output.Write(value ? "true" : "false");
    }

    public void Field(string name, DateTime value)
    {
        Key(name);
        Span<char> buffer = stackalloc char[24];
        var length = RecordWriter.FormatTime(value, buffer);
        if (length == 0)
        {
            output.Write("null");
            return;
        }

        output.Write('"');
        output.Write(buffer[..length]);
        output.Write('"');
    }

    public void EndRecord() => output.Write("}\n");

    public void Flush() => output.Flush();

    private void Key(string name)
    {
        if (!_first) output.Write(',');
        _first = false;
        output.Write('"');
        output.Write(name);
        output.Write("\":");
    }

    private void WriteEscaped(ReadOnlySpan<char> value)
    {
        while (!value.IsEmpty)
        {
            var at = value.IndexOfAny(NeedsEscape);
            if (at < 0)
            {
                output.Write(value);
                return;
            }

            output.Write(value[..at]);
            switch (value[at])
            {
                case '"': output.Write("\\\""); break;
                case '\\': output.Write("\\\\"); break;
                case '\n': output.Write("\\n"); break;
                case '\r': output.Write("\\r"); break;
                case '\t': output.Write("\\t"); break;
                default: WriteUnicodeEscape(value[at]); break; // another control character
            }

            value = value[(at + 1)..];
        }
    }

    private void WriteUnicodeEscape(char c)
    {
        Span<char> escape = stackalloc char[6];
        escape[0] = '\\';
        escape[1] = 'u';
        ((int)c).TryFormat(escape[2..], out _, "x4", CultureInfo.InvariantCulture);
        output.Write(escape);
    }
}

/// <summary>RFC 4180 CSV with a header row. A field is quoted when it holds a comma, a quote or a line break.</summary>
internal sealed class CsvWriter : IRecordWriter
{
    private static readonly SearchValues<char> NeedsQuotes = SearchValues.Create(",\"\r\n");

    private readonly TextWriter _output;
    private bool _first;

    public CsvWriter(TextWriter output, string[] columns)
    {
        _output = output;
        BeginRecord();
        foreach (var column in columns) Field(column, column);
        EndRecord();
    }

    public void BeginRecord() => _first = true;

    public void Field(string name, ReadOnlySpan<char> value)
    {
        Separate();
        if (value.IndexOfAny(NeedsQuotes) < 0)
        {
            _output.Write(value);
            return;
        }

        _output.Write('"');
        while (!value.IsEmpty)
        {
            var quote = value.IndexOf('"');
            if (quote < 0)
            {
                _output.Write(value);
                break;
            }

            _output.Write(value[..(quote + 1)]); // up to and including the quote ...
            _output.Write('"');                  // ... which is doubled
            value = value[(quote + 1)..];
        }

        _output.Write('"');
    }

    public void Field(string name, long value)
    {
        Separate();
        Span<char> buffer = stackalloc char[24];
        _output.Write(buffer[..RecordWriter.FormatLong(value, buffer)]);
    }

    public void Field(string name, bool value)
    {
        Separate();
        _output.Write(value ? "true" : "false");
    }

    public void Field(string name, DateTime value)
    {
        Separate();
        Span<char> buffer = stackalloc char[24];
        _output.Write(buffer[..RecordWriter.FormatTime(value, buffer)]); // empty for a bad time
    }

    public void EndRecord() => _output.Write('\n');

    public void Flush() => _output.Flush();

    private void Separate()
    {
        if (!_first) _output.Write(',');
        _first = false;
    }
}
