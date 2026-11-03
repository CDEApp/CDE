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
            new("regex:term", "the term is a regular expression, matched anywhere in the text, ignoring case. Quote one with a space or |: regex:\"^a|b$\""),
            new("case:term", "match case. Plain terms, wildcards and regex: ignore case unless you put case: in front"),
            new("diacritics:term", "accepted for Everything compatibility; é and e are always different here, so it changes nothing"),
            new("path:regex:term", "modifiers chain: a regular expression on the full path"),
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
        new("Wildcards",
        [
            new("*", "zero or more characters, but not a backslash: *.txt is a name that ends in .txt"),
            new("**", "zero or more characters including backslashes: path:c:\\**\\*.txt"),
            new("?", "exactly one character, but not a backslash: ?at matches cat but not chat"),
        ]),
        new("Functions",
        [
            new("size:>10mb", "size is greater than 10 MB. Also <, <=, >=, == (exactly), != (not exactly), and ranges 2mb..3mb or 2mb-3mb"),
            new("size:1kb", "a bare size matches its whole unit: 1024 to 2047 bytes. Units: b, kb, mb, gb, tb (binary, 1 kb is 1024 bytes)"),
            new("ext:mp3;flac", "files with that whole extension (; separates). Leading . and * are ignored, * and ? work. ext: alone is files with no extension"),
            new("empty:", "folders with no files or subfolders in the catalog"),
            new("child-count:>10", "folders with that many files and folders directly inside. Also child-file-count: and child-folder-count:"),
            new("descendant-count:200..300", "folders with that many files and folders at any depth. Also descendant-file-count: and descendant-folder-count:"),
            new("dupe:", "entries with the same name as another entry in any loaded catalog. dupe:size;name needs both to match"),
            new("dupe:size;hash", "same size and the hash stored in the catalog. Properties: name, path, ext, size, date-modified, hash. case:dupe: matches case"),
            new("date-modified:today", "modified today. Also yesterday, tomorrow. dm: is short for date-modified:"),
            new("dm:2026-07-02", "modified on that day. Dates are YYYY[-MM[-DD[Thh[:mm[:ss[.sss]]]]]]: dm:2026 is the whole year"),
            new("dm:2026-07-02..2026-07-03", "modified in a range, including the whole last day"),
            new("dm:3days", "modified in the last 3 days. Also secs, mins, hours, weeks, months, years. dm:>3days is newer, dm:<3days is older"),
        ]),
        new("Examples",
        [
            new("do win", "matches Windows (both do and win are in the name)"),
            new("folder:music", "folders whose name has music"),
            new("file: .mp3", "files whose name has .mp3"),
            new("path:system32 hosts", "hosts in a path that has system32"),
            new("txt | log !tmp", "names with txt, or with log but not tmp"),
            new("file: size:>100mb dm:1week", "files over 100 MB changed in the last week"),
            new("regex:^IMG_\\d+", "names that start with IMG_ and digits"),
        ]),
    ];

    public static IReadOnlyList<string> Notes { get; } =
    [
        "Terms are case-insensitive substrings of the name. A term with * or ? must match the whole name (or the whole path after path:).",
        "Include Path and the Files/Folders choice are ignored in this mode: use path:, file: and folder:.",
        "size: and date-modified: apply together with the Size and Date controls: an entry must pass both.",
        "dupe:hash uses the hash already stored by cde hash and never reads a file. A file with no full hash is left out of it; the other dupe: properties do not need hashes.",
        @"Not supported yet: other functions, size:unknown, the \: alias and the global ?path: form.",
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
