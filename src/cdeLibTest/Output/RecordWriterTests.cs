using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using cdeLib.Output;
using NUnit.Framework;

namespace cdeLibTest.Output;

/// <summary>
/// The JSON-lines and CSV record writers (#173): the same fields and the same escaping for every command, and text that
/// reads back to what was written - commas, quotes, line breaks, non-ASCII and characters JSON must escape.
/// </summary>
[TestFixture]
public class RecordWriterTests
{
    private static readonly string[] Columns = ["name", "size", "directory", "modified"];

    // Names a text format has to get right.
    private static readonly string[] AwkwardNames =
    [
        "plain.txt",
        "a,b.txt",
        "say \"hi\".txt",
        "line\nbreak.txt",
        "cr\r\nlf.txt",
        "tab\there.txt",
        "back\\slash.txt",
        "ünï-日本語-😀.txt",
        " leading and trailing ",
        "",
        "ctl\u0001char.txt",
    ];

    private static readonly DateTime When = new(2024, 3, 5, 6, 7, 8, DateTimeKind.Utc);

    private static string Write(OutputFormat format, IEnumerable<string> names)
    {
        var text = new StringWriter { NewLine = "\r\n" }; // the writers must not depend on the writer's NewLine
        var writer = RecordWriter.Create(format, text, Columns);
        long size = 0;
        foreach (var name in names)
        {
            writer.BeginRecord();
            writer.Field("name", name);
            writer.Field("size", size++);
            writer.Field("directory", size % 2 == 0);
            writer.Field("modified", When);
            writer.EndRecord();
        }

        writer.Flush();
        return text.ToString();
    }

    // ---- JSON lines ----

    [Test]
    public void Json_is_one_object_per_line_with_the_field_names_and_values_that_were_written()
    {
        var text = Write(OutputFormat.Json, AwkwardNames);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Has.Length.EqualTo(AwkwardNames.Length), "a name with a line break must not split its record");
        Assert.That(text, Does.Not.Contain("\r"), "line ends are \\n whatever the writer's NewLine, and a CR in a name is escaped");
        for (var i = 0; i < lines.Length; i++)
        {
            using var doc = JsonDocument.Parse(lines[i]);
            var o = doc.RootElement;
            Assert.That(o.GetProperty("name").GetString(), Is.EqualTo(AwkwardNames[i]));
            Assert.That(o.GetProperty("size").GetInt64(), Is.EqualTo(i));
            Assert.That(o.GetProperty("directory").GetBoolean(), Is.EqualTo((i + 1) % 2 == 0));
            Assert.That(o.GetProperty("modified").GetString(), Is.EqualTo("2024-03-05T06:07:08Z"));
        }
    }

    [Test]
    public void Json_writes_no_header_and_nothing_for_no_records()
    {
        Assert.That(Write(OutputFormat.Json, []), Is.Empty);
    }

    [Test]
    public void A_lone_surrogate_is_written_as_the_replacement_character_by_the_utf8_stream_so_the_line_stays_valid()
    {
        var bytes = new MemoryStream();
        using (var text = new StreamWriter(bytes, new UTF8Encoding(false)))
        {
            var writer = RecordWriter.Create(OutputFormat.Json, text, ["name"]);
            writer.BeginRecord();
            writer.Field("name", "bad\ud800name");
            writer.EndRecord();
        }

        using var doc = JsonDocument.Parse(bytes.ToArray());
        Assert.That(doc.RootElement.GetProperty("name").GetString(), Is.EqualTo("bad�name"));
    }

    // ---- CSV ----

    [Test]
    public void Csv_starts_with_a_header_of_the_column_names_even_with_no_records()
    {
        Assert.That(Write(OutputFormat.Csv, []), Is.EqualTo("name,size,directory,modified\n"));
    }

    [Test]
    public void Csv_reads_back_to_the_values_that_were_written()
    {
        var text = Write(OutputFormat.Csv, AwkwardNames);

        var rows = CsvText.Parse(text);
        Assert.That(rows[0], Is.EqualTo(Columns));
        Assert.That(rows, Has.Count.EqualTo(AwkwardNames.Length + 1));
        for (var i = 0; i < AwkwardNames.Length; i++)
        {
            Assert.That(rows[i + 1], Is.EqualTo(new[]
            {
                AwkwardNames[i], i.ToString(CultureInfo.InvariantCulture),
                ((i + 1) % 2 == 0) ? "true" : "false", "2024-03-05T06:07:08Z",
            }));
        }
    }

    [Test]
    public void Csv_quotes_only_the_fields_that_need_it()
    {
        var text = Write(OutputFormat.Csv, ["plain.txt", "a,b.txt", "say \"hi\".txt"]);

        Assert.That(text.Split('\n')[1], Does.StartWith("plain.txt,0,"));
        Assert.That(text, Does.Contain("\"a,b.txt\",1,"));
        Assert.That(text, Does.Contain("\"say \"\"hi\"\".txt\",2,"));
    }

    [Test]
    public void Csv_and_json_agree_on_the_values()
    {
        var fromCsv = CsvText.Parse(Write(OutputFormat.Csv, AwkwardNames)).Skip(1).Select(r => r[0]);
        var fromJson = Write(OutputFormat.Json, AwkwardNames).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("name").GetString());

        Assert.That(fromCsv, Is.EqualTo(fromJson));
    }

    private static DateTime[] BadTimes() =>
    [
        default,
        new DateTime(0, DateTimeKind.Utc),
        new DateTime(1600, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
    ];

    [TestCaseSource(nameof(BadTimes))]
    public void A_sentinel_or_out_of_range_time_is_null_in_json(DateTime bad)
    {
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Json, text, ["name", "modified"]);
        writer.BeginRecord();
        writer.Field("name", "x");
        writer.Field("modified", bad);
        writer.EndRecord();

        Assert.That(text.ToString(), Is.EqualTo("{\"name\":\"x\",\"modified\":null}\n"));
    }

    [TestCaseSource(nameof(BadTimes))]
    public void A_sentinel_or_out_of_range_time_is_an_empty_csv_field(DateTime bad)
    {
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Csv, text, ["name", "modified", "size"]);
        writer.BeginRecord();
        writer.Field("name", "x");
        writer.Field("modified", bad);
        writer.Field("size", 5);
        writer.EndRecord();

        Assert.That(text.ToString(), Is.EqualTo("name,modified,size\nx,,5\n"));
    }

    [Test]
    public void The_first_valid_time_is_still_written()
    {
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Csv, text, ["modified"]);
        writer.BeginRecord();
        writer.Field("modified", new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        writer.EndRecord();

        Assert.That(text.ToString(), Is.EqualTo("modified\n1601-01-01T00:00:00Z\n"));
    }

    [Test]
    public void A_local_time_is_written_as_utc()
    {
        var local = new DateTime(2024, 3, 5, 6, 7, 8, DateTimeKind.Local);
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Json, text, ["modified"]);
        writer.BeginRecord();
        writer.Field("modified", local);
        writer.EndRecord();

        using var doc = JsonDocument.Parse(text.ToString());
        Assert.That(DateTime.Parse(doc.RootElement.GetProperty("modified").GetString()!,
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            Is.EqualTo(local.ToUniversalTime()));
    }

    [Test]
    public void Creating_a_writer_for_the_plain_text_format_is_an_error()
    {
        Assert.Throws<ArgumentException>(() => RecordWriter.Create(OutputFormat.Text, new StringWriter(), Columns));
    }

}
