using System;
using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// <see cref="EverythingQuery"/>: the Everything-style (voidtools) query syntax, iteration 1: space (AND), |, !,
/// &lt;&gt; and "..." only. Terms are case-insensitive substrings.
/// </summary>
[TestFixture]
public class EverythingQueryTests
{
    private static bool M(string query, string text) => EverythingQuery.Parse(query).Matches(text);

    private static bool MBytes(string query, string text) =>
        EverythingQuery.Parse(query).Matches(Encoding.UTF8.GetBytes(text));

    [Test]
    public void Bare_term_is_a_case_insensitive_substring()
    {
        Assert.That(M("win", "Windows"), Is.True);
        Assert.That(M("WIN", "windows"), Is.True);
        Assert.That(M("xyz", "Windows"), Is.False);
    }

    [Test]
    public void Space_is_and_so_do_win_matches_Windows()
    {
        // 'Windows' holds both 'do' (win-DO-ws) and 'win'.
        Assert.That(M("do win", "Windows"), Is.True);
        Assert.That(M("do win", "documents"), Is.False);
        Assert.That(M("do win", "Windy"), Is.False);
        Assert.That(M("dow win", "Windows"), Is.True);
    }

    [Test]
    public void Pipe_is_or()
    {
        Assert.That(M("cat|dog", "hotdog"), Is.True);
        Assert.That(M("cat | dog", "concatenate"), Is.True);
        Assert.That(M("cat | dog", "bird"), Is.False);
    }

    [Test]
    public void Bang_negates_the_next_term()
    {
        Assert.That(M("!tmp", "readme.txt"), Is.True);
        Assert.That(M("!tmp", "a.tmp"), Is.False);
        Assert.That(M("doc !tmp", "doc.tmp"), Is.False);
        Assert.That(M("doc !tmp", "doc.txt"), Is.True);
    }

    [Test]
    public void Angle_brackets_group()
    {
        Assert.That(M("<a|b> c", "a c"), Is.True);
        Assert.That(M("<a|b> c", "b c"), Is.True);
        Assert.That(M("<a|b> c", "a"), Is.False);
        Assert.That(M("!<a b>", "a b"), Is.False);
        Assert.That(M("!<a b>", "a"), Is.True);
    }

    [Test]
    public void Quotes_make_a_literal_phrase_with_spaces_and_operators()
    {
        Assert.That(M("\"my file\"", "my file.txt"), Is.True);
        Assert.That(M("\"my file\"", "my.file"), Is.False);
        Assert.That(M("\"a|b\"", "a|b"), Is.True);
        Assert.That(M("\"a|b\"", "a"), Is.False);
        Assert.That(M("\"<x>\"", "<x>"), Is.True);
        Assert.That(M("\"!x\"", "!x"), Is.True);
    }

    [Test]
    public void And_binds_tighter_than_or()
    {
        // a b | c  ==  (a AND b) OR c
        Assert.That(M("a b | c", "c"), Is.True);
        Assert.That(M("a b | c", "ab"), Is.True);
        Assert.That(M("a b | c", "a"), Is.False);
    }

    [Test]
    public void Bang_binds_tighter_than_and()
    {
        // !a b  ==  (NOT a) AND b
        Assert.That(M("!a b", "b"), Is.True);
        Assert.That(M("!a b", "ab"), Is.False);
    }

    [Test]
    public void Bang_inside_a_word_is_literal()
    {
        Assert.That(M("a!b", "a!b.txt"), Is.True);
    }

    [Test]
    public void Empty_or_whitespace_query_matches_everything()
    {
        Assert.That(EverythingQuery.Parse("").IsEmpty, Is.True);
        Assert.That(EverythingQuery.Parse("   ").IsEmpty, Is.True);
        Assert.That(M("", "anything"), Is.True);
        Assert.That(M("  ", "anything"), Is.True);
    }

    [Test]
    public void Utf8_bytes_agree_with_the_string_path_including_non_ascii()
    {
        foreach (var (q, t) in new[]
                 {
                     ("do win", "Windows documents"), ("CAFÉ", "café.txt"), ("!é", "cafe"), ("\"x y\"", "X Y"),
                     ("a|ß", "STRASSE ß"),
                 })
        {
            Assert.That(MBytes(q, t), Is.EqualTo(M(q, t)), $"{q} vs {t}");
        }
    }

    [TestCase("\"abc", 0)]
    [TestCase("a \"bc", 2)]
    [TestCase("<a b", 0)]
    [TestCase("a b>", 3)]
    [TestCase("a |", 2)]
    [TestCase("| a", 0)]
    [TestCase("!", 0)]
    [TestCase("a !", 2)]
    [TestCase("<>", 1)]
    [TestCase("a || b", 3)]
    [TestCase("!!a", 1)]
    [TestCase("a | | b", 4)]
    [TestCase("<a |>", 4)]
    public void Malformed_query_is_an_error_with_a_position(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex.Position, Is.EqualTo(position), ex.Message);
        Assert.That(ex.Message, Does.Contain(position.ToString()));
        Assert.That(ex, Is.InstanceOf<System.FormatException>());
    }

    [Test]
    public void Nesting_up_to_the_cap_parses_and_beyond_it_is_an_error()
    {
        var ok = new string('<', EverythingQuery.MaxDepth) + "a" + new string('>', EverythingQuery.MaxDepth);
        Assert.That(M(ok, "a"), Is.True);

        var tooDeep = new string('<', EverythingQuery.MaxDepth + 1) + "a" +
                      new string('>', EverythingQuery.MaxDepth + 1);
        Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(tooDeep));
    }

    [Test]
    public void Operators_inside_quotes_do_not_count_towards_nesting()
    {
        var q = "\"" + new string('<', 500) + "\"";
        Assert.That(M(q, new string('<', 500)), Is.True);
    }

    // ---- path: and no-path: modifiers ----

    private const string FullPath = @"C:\Windows\System32\hosts.txt";
    private const string Name = "hosts.txt";

    private static bool P(string query, bool defaultMatchesPath = false) =>
        EverythingQuery.Parse(query).Matches(Name, FullPath, defaultMatchesPath);

    [Test]
    public void Path_modifier_matches_the_full_path_even_when_the_default_is_the_name()
    {
        Assert.That(P("system32"), Is.False);
        Assert.That(P("path:system32"), Is.True);
        Assert.That(P("PATH:SYSTEM32"), Is.True, "modifier names and terms are case-insensitive");
        Assert.That(P("path:hosts"), Is.True, "the full path includes the name");
    }

    [Test]
    public void No_path_modifier_forces_the_name_even_when_the_default_is_the_path()
    {
        Assert.That(P("system32", defaultMatchesPath: true), Is.True);
        Assert.That(P("no-path:system32", defaultMatchesPath: true), Is.False);
        Assert.That(P("No-Path:hosts", defaultMatchesPath: true), Is.True);
    }

    [Test]
    public void Modifier_applies_to_one_term_only()
    {
        Assert.That(P("path:system32 hosts"), Is.True);
        Assert.That(P("path:system32 windows"), Is.False, "windows is only in the path and is a name term");
        Assert.That(P("hosts path:windows"), Is.True);
    }

    [Test]
    public void Modifier_applies_to_a_phrase_and_to_a_group()
    {
        Assert.That(P("path:\"windows\\system32\""), Is.True);
        Assert.That(P("path:<windows | nothere> hosts"), Is.True);
        Assert.That(P("path:<windows system32>"), Is.True);
        Assert.That(P("<windows | nothere>"), Is.False, "without the modifier the group is name-only");
    }

    [Test]
    public void Innermost_modifier_wins_inside_a_group()
    {
        Assert.That(P("path:<windows no-path:hosts>"), Is.True);
        Assert.That(P("path:<windows no-path:system32>"), Is.False);
    }

    [Test]
    public void Bang_goes_in_front_of_the_modifier()
    {
        Assert.That(P("!path:system32"), Is.False);
        Assert.That(P("!path:nothere"), Is.True);
        Assert.That(P("hosts !path:windows"), Is.False);
    }

    [Test]
    public void Quotes_make_path_colon_literal()
    {
        Assert.That(EverythingQuery.Parse("\"path:x\"").Matches("a path:x b"), Is.True);
        Assert.That(EverythingQuery.Parse("mypath:x").Matches("mypath:x"), Is.True, "only at the start of a term");
    }

    [TestCase("path:", 0)]
    [TestCase("a path:", 2)]
    [TestCase("path: a", 0)]
    [TestCase("path:|a", 0)]
    [TestCase("path:!a", 0)]
    [TestCase("path:no-path:a", 0)]
    [TestCase("no-path:", 0)]
    [TestCase("?path:a", 0)]
    [TestCase("a ?no-path:b", 2)]
    [TestCase("\\:a", 0)]
    public void Malformed_or_unsupported_modifier_is_an_error_with_a_position(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex.Position, Is.EqualTo(position), ex.Message);
    }

    [Test]
    public void Unsupported_forms_say_so()
    {
        Assert.That(Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("?path:a")).Message,
            Does.Contain("not supported"));
        Assert.That(Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("\\:a")).Message,
            Does.Contain("not supported"));
    }

    [Test]
    public void Utf8_and_char_paths_agree_for_modifiers()
    {
        var q = EverythingQuery.Parse("path:café !no-path:zzz");
        var name = "naïve.txt";
        var path = "C:\\café\\naïve.txt";
        Assert.That(q.Matches(name, path, false), Is.True);
        Assert.That(q.Matches(new Utf8Entry(name, path), false), Is.True);
    }

    // ---- file: and folder: filters ----

    // Each argument is a name; the entry is a folder when isFolder, else a file.
    private static bool K(string query, string name, bool isFolder) =>
        EverythingQuery.Parse(query).Matches(name, @"C:\x\" + name, false, isFolder);

    [Test]
    public void Folder_filter_keeps_folders_and_file_filter_keeps_files()
    {
        Assert.That(K("folder:music", "Music", isFolder: true), Is.True);
        Assert.That(K("folder:music", "Music", isFolder: false), Is.False, "a file named Music is not a folder");
        Assert.That(K("file: .mp3", "a.mp3", isFolder: false), Is.True);
        Assert.That(K("file: .mp3", "a.mp3", isFolder: true), Is.False, "a folder named a.mp3 is not a file");
    }

    [Test]
    public void Filter_names_are_case_insensitive_and_the_text_after_the_colon_is_its_own_term()
    {
        Assert.That(K("FOLDER:Music", "music", isFolder: true), Is.True);
        Assert.That(K("File:MUSIC", "music", isFolder: false), Is.True);
        Assert.That(K("folder:music", "videos", isFolder: true), Is.False, "folder: AND music");
        Assert.That(K("folder: music", "music", isFolder: true), Is.EqualTo(K("folder:music", "music", isFolder: true)));
    }

    [Test]
    public void Filter_alone_is_valid_and_matches_every_entry_of_that_kind()
    {
        var files = EverythingQuery.Parse("file:");
        Assert.That(files.IsEmpty, Is.False, "it is a real filter, not an empty query");
        Assert.That(files.Matches("anything", @"C:\anything", false, isFolder: false), Is.True);
        Assert.That(files.Matches("anything", @"C:\anything", false, isFolder: true), Is.False);
        Assert.That(EverythingQuery.Parse("folder:").Matches("x", @"C:\x", false, isFolder: true), Is.True);
    }

    [Test]
    public void File_and_folder_together_match_nothing_without_an_error()
    {
        Assert.That(K("file: folder:", "x", isFolder: true), Is.False);
        Assert.That(K("file: folder:", "x", isFolder: false), Is.False);
    }

    [Test]
    public void Filters_compose_with_not_or_and_groups()
    {
        Assert.That(K("!file:", "x", isFolder: true), Is.True, "!file: is folders");
        Assert.That(K("!file:", "x", isFolder: false), Is.False);
        Assert.That(K("<file: a> | <folder: b>", "a", isFolder: false), Is.True);
        Assert.That(K("<file: a> | <folder: b>", "b", isFolder: true), Is.True);
        Assert.That(K("<file: a> | <folder: b>", "a", isFolder: true), Is.False);
        Assert.That(K("<file: a> | <folder: b>", "b", isFolder: false), Is.False);
        Assert.That(K("!folder:music", "music", isFolder: false), Is.True, "a bang applies to the filter only");
    }

    [Test]
    public void Filters_work_with_path_terms_and_the_entry_overloads()
    {
        Assert.That(EverythingQuery.Parse("folder: path:x").Matches("m", @"C:\x\m", false, isFolder: true), Is.True);
        Assert.That(EverythingQuery.Parse("folder: path:x").Matches("m", @"C:\y\m", false, isFolder: true), Is.False);
        var q = EverythingQuery.Parse("file: path:café");
        Assert.That(q.Matches(new Utf8Entry("n.txt", @"C:\café\n.txt", isFolder: false), false), Is.True);
        Assert.That(q.Matches(new Utf8Entry("n.txt", @"C:\café\n.txt", isFolder: true), false), Is.False);
    }

    [Test]
    public void Quotes_make_filter_colons_literal_and_longer_words_are_not_filters()
    {
        Assert.That(EverythingQuery.Parse("\"file:\"").Matches("my file: here", isFolder: true), Is.True);
        Assert.That(M("files:x", "files:x"), Is.True, "files: is just a word");
        Assert.That(M("myfile:x", "myfile:x"), Is.True, "only at the start of a term");
    }

    [TestCase("path:file:", 0)]
    [TestCase("a path:folder:b", 2)]
    public void A_filter_is_not_a_term_a_path_modifier_can_attach_to(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex.Position, Is.EqualTo(position), ex.Message);
    }

    [Test]
    public void A_filter_inside_a_group_after_path_is_fine_and_the_filter_ignores_the_modifier()
    {
        var q = EverythingQuery.Parse("path:<file: x>");
        Assert.That(q.Matches("a", @"C:\x\a", false, isFolder: false), Is.True);
        Assert.That(q.Matches("a", @"C:\x\a", false, isFolder: true), Is.False);
    }

    private sealed class Utf8Entry(string name, string path, bool isFolder = false) : IEntryText
    {
        public bool IsFolder => isFolder;
        private readonly byte[] _name = Encoding.UTF8.GetBytes(name);
        private readonly byte[] _path = Encoding.UTF8.GetBytes(path);
        public ReadOnlySpan<byte> Name => _name;
        public ReadOnlySpan<byte> Path => _path;
    }
}
