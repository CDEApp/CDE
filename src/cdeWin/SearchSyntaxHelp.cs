using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace cdeWin;

/// <summary>One line of the Search Syntax help: what to type and what it does.</summary>
public sealed record HelpRow(string Syntax, string Meaning);

public sealed record HelpSection(string Title, IReadOnlyList<HelpRow> Rows);

/// <summary>The text of Help &gt; Search Syntax: the Everything-style operators, modifiers and filters.</summary>
public static class SearchSyntaxHelp
{
    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        new("Operators",
        [
            new("a b", "space is AND: the name has both a and b"),
            new("a | b", "OR: the name has a or b (a space binds tighter than |)"),
            new("!a", "NOT: the name does not have a"),
            new("<a | b> c", "group with < >: the name has a or b, and also c"),
            new("\"a b\"", "phrase: the exact text a b, and operators inside the quotes are plain text"),
        ]),
        new("Modifiers",
        [
            new("path:term", "match the full path, not just the name. Directly in front of one word, phrase or group, with no space"),
            new("no-path:term", "match the name only"),
            new("path:<a | b>", "a modifier can go in front of a group; the innermost modifier wins"),
            new("!path:term", "put ! in front of the modifier to negate it"),
        ]),
        new("Filters",
        [
            new("file:", "files only"),
            new("folder:", "folders only"),
            new("!file:", "NOT a file, so folders"),
            new("<file: a> | <folder: b>", "files with a, or folders with b"),
        ]),
        new("Examples",
        [
            new("do win", "matches Windows (both do and win are in the name)"),
            new("folder:music", "folders whose name has music"),
            new("file: .mp3", "files whose name has .mp3"),
            new("path:system32 hosts", "hosts in a path that has system32"),
            new("txt | log !tmp", "names with txt, or with log but not tmp"),
        ]),
    ];

    public static IReadOnlyList<string> Notes { get; } =
    [
        "Terms are case-insensitive substrings of the name.",
        "Include Path and the Files/Folders choice are ignored in this mode: use path:, file: and folder:.",
        @"Not supported yet: wildcards, ext: and other functions, the \: alias and the global ?path: form.",
    ];

    /// <summary>All sections and notes as one text for a read-only box, with the syntax column aligned.</summary>
    public static string ToText()
    {
        var width = Sections.SelectMany(s => s.Rows).Max(r => r.Syntax.Length) + 2;
        var sb = new StringBuilder();
        foreach (var section in Sections)
        {
            sb.AppendLine(section.Title).AppendLine(new string('-', section.Title.Length));
            foreach (var row in section.Rows) sb.AppendLine(row.Syntax.PadRight(width) + row.Meaning);
            sb.AppendLine();
        }

        sb.AppendLine("Notes");
        sb.AppendLine("-----");
        foreach (var note in Notes) sb.AppendLine(note);
        return sb.ToString();
    }
}
