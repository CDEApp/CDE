using System.Linq;
using cdeWin;
using NUnit.Framework;

namespace cdeWinTest;

/// <summary>Help &gt; Search Syntax text: it must list every supported operator, modifier and filter.</summary>
[TestFixture]
public class SearchSyntaxHelpTests
{
    [Test]
    public void Has_the_four_sections_in_order()
    {
        Assert.That(SearchSyntaxHelp.Sections.Select(s => s.Title),
            Is.EqualTo(new[] { "Operators", "Modifiers", "Filters", "Examples" }));
    }

    [Test]
    public void Lists_every_supported_operator_modifier_and_filter()
    {
        var syntaxes = string.Join("\n", SearchSyntaxHelp.Sections.SelectMany(s => s.Rows).Select(r => r.Syntax));
        foreach (var item in new[] { "a b", "|", "!", "<", "\"", "path:", "no-path:", "file:", "folder:" })
            Assert.That(syntaxes, Does.Contain(item));
        Assert.That(SearchSyntaxHelp.Sections[0].Rows[0].Meaning, Does.Contain("space is AND"));
    }

    [Test]
    public void Every_row_has_a_meaning()
    {
        foreach (var row in SearchSyntaxHelp.Sections.SelectMany(s => s.Rows))
            Assert.That(row.Meaning.Trim(), Is.Not.Empty, row.Syntax);
    }

    [Test]
    public void Includes_the_worked_examples_from_the_feature_request()
    {
        var syntaxes = SearchSyntaxHelp.Sections.SelectMany(s => s.Rows).Select(r => r.Syntax).ToList();
        Assert.That(syntaxes, Does.Contain("do win"));
        Assert.That(syntaxes, Does.Contain("folder:music"));
        Assert.That(syntaxes, Does.Contain("file: .mp3"));
    }

    [Test]
    public void Says_what_is_not_supported_and_which_controls_are_ignored()
    {
        var notes = string.Join(" ", SearchSyntaxHelp.Notes);
        foreach (var item in new[] { "wildcards", "ext:", @"\:", "?path:", "Include Path", "Files/Folders" })
            Assert.That(notes, Does.Contain(item));
    }

    [Test]
    public void ToText_puts_titles_rows_and_notes_in_one_readable_text()
    {
        var text = SearchSyntaxHelp.ToText();

        Assert.That(text, Does.Contain("Operators"));
        Assert.That(text, Does.Contain("folder:music"));
        Assert.That(text, Does.Contain("Not supported yet:"));
        Assert.That(text.IndexOf("Operators"), Is.LessThan(text.IndexOf("Examples")));
    }
}
