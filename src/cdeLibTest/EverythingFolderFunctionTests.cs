using System;
using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>The functions <c>ext:</c>, <c>empty:</c>, <c>child-count:</c> and <c>descendant-count:</c> of <see cref="EverythingQuery"/>.</summary>
[TestFixture]
public class EverythingFolderFunctionTests
{
    private static void Check(string query, string name, bool isFolder, bool expected)
    {
        Assert.That(EverythingQuery.Parse(query).Matches(name, isFolder), Is.EqualTo(expected), "chars");
        Assert.That(EverythingQuery.Parse(query).Matches(Encoding.UTF8.GetBytes(name), isFolder), Is.EqualTo(expected), "bytes");
    }

    [TestCase("ext:mp3", "a.mp3", true)]
    [TestCase("ext:mp3", "A.MP3", true)]
    [TestCase("ext:mp3", "a.mp3x", false)]
    [TestCase("ext:mp3", "mp3", false)]
    [TestCase("ext:mp3", "a.b.mp3", true)]
    [TestCase("ext:mp3", "a.mp3.bak", false)]
    [TestCase("ext:mp3;flac", "a.flac", true)]
    [TestCase("ext:mp3;flac", "a.ogg", false)]
    [TestCase("ext:.mp3", "a.mp3", true)]
    [TestCase("ext:*.mp3", "a.mp3", true)]
    [TestCase("ext:*mp3", "a.mp3", true)]
    [TestCase("ext:mp*", "a.mp4", true)]
    [TestCase("ext:mp*", "a.mpeg", true)]
    [TestCase("ext:mp*", "a.m4a", false)]
    [TestCase("ext:m?3", "a.mp3", true)]
    [TestCase("ext:*.txt;*.doc*", "a.docx", true)]
    [TestCase("ext:*.txt;*.doc*", "a.txt", true)]
    [TestCase("ext:*.txt;*.doc*", "a.pdf", false)]
    [TestCase("ext:", "README", true)]
    [TestCase("ext:", ".gitignore", true)]
    [TestCase("ext:", "a.", true)]
    [TestCase("ext:", "a.txt", false)]
    [TestCase("ext:*", "README", true)]
    [TestCase("ext:*", "a.txt", true)]
    [TestCase("ext:caf\u00e9", "a.CAF\u00c9", true)]
    [TestCase("ext:caf\u00e9", "a.cafe", false)]
    [TestCase("case:ext:MP3", "a.mp3", false)]
    [TestCase("case:ext:MP3", "a.MP3", true)]
    [TestCase("case:ext:M*", "a.mp3", false)]
    [TestCase("case:ext:M*", "a.MP3", true)]
    [TestCase("diacritics:ext:caf\u00e9", "a.caf\u00e9", true)]
    [TestCase("!ext:mp3", "a.mp3", false)]
    [TestCase("!ext:mp3", "a.wav", true)]
    [TestCase("ext:mp3 a", "a.mp3", true)]
    [TestCase("ext:mp3 b", "a.mp3", false)]
    public void Ext_matches_the_whole_extension_of_a_file(string query, string name, bool expected) =>
        Check(query, name, isFolder: false, expected);

    [Test]
    public void Ext_never_matches_a_folder_but_can_be_combined_with_folder()
    {
        Check("ext:mp3", "music.mp3", isFolder: true, false);
        Check("ext:", "music", isFolder: true, false);
        Check("ext:mp3 | folder:", "music", isFolder: true, true);
        Check("ext:mp3 | folder:", "a.wav", isFolder: false, false);
        Check("ext:mp3 | folder:", "a.mp3", isFolder: false, true);
    }

    [Test]
    public void Ext_reads_the_name_even_when_other_terms_read_the_path()
    {
        var query = EverythingQuery.Parse("path:music ext:mp3");
        Assert.That(query.TextsRead(defaultMatchesPath: false), Is.EqualTo(EverythingTexts.Both));
        Assert.That(query.Matches("a.mp3", @"C:\music\a.mp3", defaultMatchesPath: false), Is.True);
        Assert.That(query.Matches("a.mp3", @"C:\other\a.mp3", defaultMatchesPath: false), Is.False);
        Assert.That(query.Matches("a.wav", @"C:\music\a.wav", defaultMatchesPath: false), Is.False);
        Assert.That(EverythingQuery.Parse("ext:mp3").TextsRead(defaultMatchesPath: true), Is.EqualTo(EverythingTexts.Name));
    }

    [TestCase("ext:;", 0)]
    [TestCase("a ext:;;", 2)]
    [TestCase("path:ext:mp3", 0)]
    [TestCase("regex:ext:mp3", 0)]
    [TestCase("case:size:>1", 0)]
    [TestCase("no-path:case:empty:", 0)]
    [TestCase("case:diacritics:empty:", 5)]
    public void A_bad_ext_or_a_modifier_that_cannot_precede_a_function_is_an_error(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Position, Is.EqualTo(position), ex.Message);
    }

    // ---- counts ----

    private static bool Counted(string query, EntryCounts counts, bool isFolder = true) =>
        EverythingQuery.Parse(query).Matches("x", isFolder, counts: counts);

    private static readonly EntryCounts Empty = new(0, 0, 0, 0);
    private static readonly EntryCounts Mixed = new(2, 1, 12, 4); // 2 files and 1 folder below, 12 files and 4 folders in all

    [TestCase("empty:", true, false)]
    [TestCase("child-count:0", true, false)]
    [TestCase("child-count:3", false, true)]
    [TestCase("child-count:2", false, false)]
    [TestCase("child-count:>2", false, true)]
    [TestCase("child-count:1..3", false, true)]
    [TestCase("child-count:4..9", false, false)]
    [TestCase("child-count:!=3", true, false)]
    [TestCase("child-file-count:2", false, true)]
    [TestCase("child-file-count:1", false, false)]
    [TestCase("child-folder-count:1", false, true)]
    [TestCase("child-file-count:2 child-folder-count:1", false, true)]
    [TestCase("child-file-count:1 child-folder-count:0", false, false)]
    [TestCase("childcount:3", false, true)]
    [TestCase("descendant-count:16", false, true)]
    [TestCase("descendant-count:>1000", false, false)]
    [TestCase("descendant-count:10..20", false, true)]
    [TestCase("descendant-file-count:12", false, true)]
    [TestCase("descendant-folder-count:<5", true, true)]
    [TestCase("descendant-folder-count:4", false, true)]
    [TestCase("descendantcount:0", true, false)]
    public void Count_functions(string query, bool emptyFolder, bool mixedFolder)
    {
        Assert.That(Counted(query, Empty), Is.EqualTo(emptyFolder), "empty folder");
        Assert.That(Counted(query, Mixed), Is.EqualTo(mixedFolder), "mixed folder");
    }

    [Test]
    public void Count_functions_test_folders_only()
    {
        Assert.That(Counted("empty:", Empty, isFolder: false), Is.False);
        Assert.That(Counted("child-count:0", Empty, isFolder: false), Is.False);
        Assert.That(Counted("descendant-count:<5", Empty, isFolder: false), Is.False);
    }

    [Test]
    public void Count_functions_combine_with_the_other_operators()
    {
        Assert.That(Counted("folder: !empty:", Mixed), Is.True);
        Assert.That(Counted("folder: !empty:", Empty), Is.False);
        Assert.That(Counted("empty: | child-count:3", Mixed), Is.True);
        Assert.That(Counted("<empty: | child-count:3> x", Mixed), Is.True);
        Assert.That(EverythingQuery.Parse("a").UsesCounts, Is.False);
        Assert.That(EverythingQuery.Parse("empty:").UsesCounts, Is.True);
        Assert.That(EverythingQuery.Parse("empty:").UsesDescendantCounts, Is.False);
        Assert.That(EverythingQuery.Parse("a | child-file-count:1").UsesDescendantCounts, Is.False);
        Assert.That(EverythingQuery.Parse("!descendant-count:1").UsesDescendantCounts, Is.True);
    }

    [TestCase("empty:x", 0)]
    [TestCase("a empty:x", 2)]
    [TestCase("child-count:", 0)]
    [TestCase("child-count:abc", 0)]
    [TestCase("child-count:-1", 0)]
    [TestCase("child-count:1.5", 0)]
    [TestCase("descendant-count:99999999999", 0)]
    [TestCase("child-count:5..", 0)]
    public void A_bad_count_is_an_error_at_its_position(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Position, Is.EqualTo(position), ex.Message);
    }

    // ---- EntryCountTable ----

    [Test]
    public void EntryCountTable_counts_children_and_every_descendant()
    {
        // 0 root, 1 a, 2 a/b, 3 a/b/x.txt, 4 a/y.txt, 5 z.txt, 6 void
        var parents = new[] { -1, 0, 1, 2, 1, 0, 0 };
        var folders = new[] { true, true, true, false, false, false, true };
        var table = EntryCountTable.FromParents(parents, folders, descendants: true);

        Assert.That(table.Of(0), Is.EqualTo(new EntryCounts(1, 2, 3, 3)));
        Assert.That(table.Of(1), Is.EqualTo(new EntryCounts(1, 1, 2, 1)));
        Assert.That(table.Of(2), Is.EqualTo(new EntryCounts(1, 0, 1, 0)));
        Assert.That(table.Of(6), Is.EqualTo(new EntryCounts(0, 0, 0, 0)));
        Assert.That(table.Of(3), Is.EqualTo(new EntryCounts(0, 0, 0, 0)));
    }

    [Test]
    public void EntryCountTable_gives_the_same_counts_when_a_parent_comes_after_its_child()
    {
        // The same tree as above with entries 2 and 3 in the other order: 0 root, 1 a, 2 a/b/x.txt, 3 a/b, 4 a/y.txt ...
        var parents = new[] { -1, 0, 3, 1, 1, 0, 0 };
        var folders = new[] { true, true, false, true, false, false, true };
        var table = EntryCountTable.FromParents(parents, folders, descendants: true);

        Assert.That(table.Of(0), Is.EqualTo(new EntryCounts(1, 2, 3, 3)));
        Assert.That(table.Of(1), Is.EqualTo(new EntryCounts(1, 1, 2, 1)));
        Assert.That(table.Of(3), Is.EqualTo(new EntryCounts(1, 0, 1, 0)));
    }

    [Test]
    public void EntryCountTable_survives_a_corrupt_parent_cycle()
    {
        var parents = new[] { -1, 2, 1 }; // 1 and 2 are each other's parent
        var folders = new[] { true, true, true };
        Assert.DoesNotThrow(() => EntryCountTable.FromParents(parents, folders, descendants: true));
    }

    [Test]
    public void EntryCountTable_leaves_descendants_out_when_not_asked_for()
    {
        var table = EntryCountTable.FromParents([-1, 0, 1], [true, true, false], descendants: false);
        Assert.That(table.Of(0), Is.EqualTo(new EntryCounts(0, 1, 0, 0)));
    }
}
