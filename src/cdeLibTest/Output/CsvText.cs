using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace cdeLibTest.Output;

/// <summary>An RFC 4180 reader written for the tests, independent of the writer: quoted fields hold commas, quotes ("") and line breaks.</summary>
internal static class CsvText
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else field.Append(c);
        }

        Assert.That(quoted, Is.False, "unterminated quote");
        Assert.That(row, Is.Empty, "text must end with a record separator");
        return rows;
    }
}
