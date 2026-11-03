using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary><see cref="WildcardPattern"/>: voidtools wildcards, matched against the whole text.</summary>
[TestFixture]
public class WildcardPatternTests
{
    private static bool M(string pattern, string text) => WildcardPattern.Parse(pattern).IsMatch(text);

    [TestCase("*.txt", "notes.txt", true)]
    [TestCase("*.txt", "notes.txt.bak", false)]
    [TestCase("*.txt", ".txt", true)]
    [TestCase("a*b", "ab", true)]
    [TestCase("a*b", "axxb", true)]
    [TestCase("a*b", "xab", false)]
    [TestCase("a*b", "abx", false)]
    [TestCase("foo*", "foobar", true)]
    [TestCase("foo*", "xfoo", false)]
    [TestCase("?at", "cat", true)]
    [TestCase("?at", "at", false)]
    [TestCase("?at", "chat", false)]
    [TestCase("*", "", true)]
    [TestCase("*", "anything", true)]
    [TestCase("**", "", true)]
    [TestCase("***", @"a\b", true)]
    [TestCase("?", "", false)]
    [TestCase("", "", true)]
    [TestCase("", "x", false)]
    [TestCase("*a*a*a*", "banana", true)]
    [TestCase("*a*a*a*a*", "banana", false)]
    public void Matches_the_whole_text(string pattern, string text, bool expected) =>
        Assert.That(M(pattern, text), Is.EqualTo(expected));

    [Test]
    public void Star_stops_at_a_backslash_but_double_star_crosses_it()
    {
        Assert.That(M("a*b", @"a\b"), Is.False);
        Assert.That(M("a**b", @"a\b"), Is.True);
        Assert.That(M(@"c:\*\x", @"c:\dir\x"), Is.True);
        Assert.That(M(@"c:\*\x", @"c:\dir\sub\x"), Is.False);
        Assert.That(M(@"c:\**\x", @"c:\dir\sub\x"), Is.True);
    }

    [Test]
    public void Question_mark_does_not_match_a_backslash()
    {
        Assert.That(M("a?b", @"a\b"), Is.False);
        Assert.That(M("a?b", "axb"), Is.True);
        Assert.That(M("a??b", "axyb"), Is.True);
    }

    [Test]
    public void A_later_star_cannot_cover_a_backslash_so_an_earlier_double_star_must_stretch()
    {
        // A matcher that backtracks only to the last star fails here: '**' has to absorb 'xa\' so '*' can sit before 'b'.
        Assert.That(M("**a*b", @"xa\ab"), Is.True);
        Assert.That(M("**a*b", @"xa\a\b"), Is.False);
    }

    [Test]
    public void Is_case_insensitive_including_non_ascii()
    {
        Assert.That(M("*.TXT", "Notes.txt"), Is.True);
        Assert.That(M("CAF\u00c9*", "caf\u00e9 au lait"), Is.True);
        Assert.That(M("*\u30d5\u30a1\u30a4\u30eb*", "x\u30d5\u30a1\u30a4\u30eby"), Is.True);
        Assert.That(M("na\u00cfve?", "NA\u00efVE!"), Is.True);
    }

    [Test]
    public void Pathological_patterns_finish_quickly_on_long_text()
    {
        var text = new string('a', 20_000);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.That(M("*a*a*a*a*a*a*a*a*b", text), Is.False);
        Assert.That(M("*a*a*a*a*a*a*a*a*", text), Is.True);
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void IsWildcard_is_true_only_for_star_or_question_mark()
    {
        Assert.That(WildcardPattern.IsWildcard("a*"), Is.True);
        Assert.That(WildcardPattern.IsWildcard("a?"), Is.True);
        Assert.That(WildcardPattern.IsWildcard("abc.txt"), Is.False);
    }
}
