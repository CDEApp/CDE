using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>The <c>case:</c> and <c>diacritics:</c> modifiers of <see cref="EverythingQuery"/>.</summary>
[TestFixture]
public class EverythingCaseTests
{
    private static void Check(string query, string text, bool expected)
    {
        Assert.That(EverythingQuery.Parse(query).Matches(text), Is.EqualTo(expected), "chars");
        Assert.That(EverythingQuery.Parse(query).Matches(Encoding.UTF8.GetBytes(text)), Is.EqualTo(expected), "bytes");
    }

    [TestCase("abc", "ABC", true)]
    [TestCase("case:abc", "ABC", false)]
    [TestCase("case:abc", "xabcx", true)]
    [TestCase("case:ABC", "abc", false)]
    [TestCase("case:caf\u00e9", "caf\u00e9", true)]
    [TestCase("case:caf\u00e9", "CAF\u00c9", false)]
    [TestCase("case:caf\u00e9", "cafe", false)]
    [TestCase("case:\u00e9", "plain ascii", false)]
    [TestCase("case:abc", "xx\u00e9 abc", true)]
    [TestCase("case:abc", "xx\u00e9 ABC", false)]
    [TestCase("*.TXT", "a.txt", true)]
    [TestCase("case:*.TXT", "a.txt", false)]
    [TestCase("case:*.TXT", "a.TXT", true)]
    [TestCase("case:a?c", "aBc", true)]
    [TestCase("case:a?c", "Abc", false)]
    [TestCase("regex:^a", "ABC", true)]
    [TestCase("case:regex:^a", "ABC", false)]
    [TestCase("case:regex:^A", "ABC", true)]
    [TestCase("regex:case:^A", "abc", false)]
    [TestCase("case:\"a b\"", "A B", false)]
    [TestCase("case:\"a b\"", "a b", true)]
    public void Case_modifier(string query, string text, bool expected) => Check(query, text, expected);

    [Test]
    public void Case_applies_only_to_the_term_it_precedes_and_to_a_whole_group()
    {
        Check("case:abc DEF", "abc def", true);
        Check("case:abc DEF", "ABC def", false);
        Check("case:<abc def>", "abc def", true);
        Check("case:<abc def>", "abc DEF", false);
        Check("!case:abc", "ABC", true);
        Check("!case:abc", "abc", false);
    }

    [Test]
    public void Case_chains_with_the_other_modifiers()
    {
        var entry = new StringEntry("Report.txt", @"C:\Docs\Report.txt");
        Assert.That(EverythingQuery.Parse("case:path:Docs").Matches(entry.Name, entry.Path, false), Is.True);
        Assert.That(EverythingQuery.Parse("case:path:docs").Matches(entry.Name, entry.Path, false), Is.False);
        Assert.That(EverythingQuery.Parse("path:case:Docs").Matches(entry.Name, entry.Path, false), Is.True);
        Assert.That(EverythingQuery.Parse("case:no-path:report").Matches(entry.Name, entry.Path, true), Is.False);
    }

    // Diacritics always count in CDE, so the modifier is accepted for Everything compatibility and changes nothing.
    [TestCase("diacritics:e", "caf\u00e9", false)]
    [TestCase("diacritics:e", "cafe", true)]
    [TestCase("diacritics:caf\u00e9", "CAF\u00c9", true)]
    [TestCase("diacritics:case:caf\u00e9", "CAF\u00c9", false)]
    [TestCase("case:diacritics:caf\u00e9", "caf\u00e9", true)]
    public void Diacritics_modifier_is_accepted_and_changes_nothing(string query, string text, bool expected) =>
        Check(query, text, expected);

    [TestCase("case:", 0)]
    [TestCase("case: abc", 0)]
    [TestCase("a diacritics:", 2)]
    [TestCase("case:|a", 0)]
    public void A_case_modifier_must_be_attached_to_its_term(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Position, Is.EqualTo(position));
    }

    private sealed record StringEntry(string Name, string Path);
}
