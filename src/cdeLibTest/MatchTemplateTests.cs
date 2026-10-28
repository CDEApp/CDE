using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// <see cref="MatchTemplate"/>: parsing of the <c>--template</c> text and rendering one match line from it.
/// </summary>
[TestFixture]
public class MatchTemplateTests
{
    private const string Catalog = "C-Vol-C__data";
    private const string CatalogFile = Catalog + ".cdex";

    private static string Render(string template, string fullPath, string catalog = Catalog) =>
        MatchTemplate.Parse(template).Render(catalog, fullPath);

    [Test]
    public void FullPath_placeholder_is_the_whole_path()
    {
        Assert.That(Render("{fullpath}", @"C:\data\docs\a.txt"), Is.EqualTo(@"C:\data\docs\a.txt"));
    }

    [Test]
    public void Catalog_placeholder_is_the_catalog_file_name_without_its_directory_or_extension()
    {
        Assert.That(Render("{catalog}", "x", @"D:\cats\" + CatalogFile), Is.EqualTo(Catalog));
        Assert.That(Render("{catalog}", "x", "/home/me/cats/" + CatalogFile), Is.EqualTo(Catalog));
        Assert.That(Render("{catalog}", "x", CatalogFile), Is.EqualTo(Catalog));
        Assert.That(Render("{catalog}", "x", "legacy.cde"), Is.EqualTo("legacy"));
    }

    [Test]
    public void Catalog_placeholder_drops_only_the_last_extension_and_keeps_a_name_with_none()
    {
        Assert.That(Render("{catalog}", "x", "a.b.cdex"), Is.EqualTo("a.b"));
        Assert.That(Render("{catalog}", "x", "noext"), Is.EqualTo("noext"));
        Assert.That(Render("{catalog}", "x", @"D:\dir.d\noext"), Is.EqualTo("noext"));
    }

    [Test]
    public void Filename_placeholder_is_the_last_segment_including_extension()
    {
        Assert.That(Render("{filename}", @"C:\data\docs\a.txt"), Is.EqualTo("a.txt"));
    }

    [Test]
    public void Directory_placeholder_is_the_parent_folder_without_a_trailing_separator()
    {
        Assert.That(Render("{directory}", @"C:\data\docs\a.txt"), Is.EqualTo(@"C:\data\docs"));
    }

    [Test]
    public void Directory_and_filename_split_on_forward_slashes_too()
    {
        Assert.That(Render("{directory}|{filename}", "/mnt/data/docs/a.txt"), Is.EqualTo("/mnt/data/docs|a.txt"));
    }

    [Test]
    public void A_path_scanned_on_the_other_os_splits_on_its_own_separator()
    {
        // a Windows path rendered where Path.DirectorySeparatorChar is '/'
        Assert.That(Render("{filename}", @"C:\data\a.txt"), Is.EqualTo("a.txt"));
    }

    [Test]
    public void Folder_entry_uses_its_own_name_and_its_parent()
    {
        Assert.That(Render("{directory}|{filename}", @"C:\data\docs"), Is.EqualTo(@"C:\data|docs"));
    }

    [Test]
    public void Root_level_entry_has_the_root_as_its_directory()
    {
        Assert.That(Render("{directory}|{filename}", @"C:\a.txt"), Is.EqualTo("C:|a.txt"));
    }

    [Test]
    public void Path_with_no_separator_is_all_filename_and_an_empty_directory()
    {
        Assert.That(Render("[{directory}][{filename}]", "a.txt"), Is.EqualTo("[][a.txt]"));
    }

    [Test]
    public void Placeholders_mix_with_literal_text()
    {
        Assert.That(Render("{catalog} {fullpath}", @"C:\data\a.txt"), Is.EqualTo(Catalog + @" C:\data\a.txt"));
        Assert.That(Render("{catalog} {fullpath}", @"C:\data\a.txt", CatalogFile), Is.EqualTo(Catalog + @" C:\data\a.txt"));
    }

    [Test]
    public void The_same_placeholder_can_appear_more_than_once()
    {
        Assert.That(Render("{filename}={filename}", @"C:\a.txt"), Is.EqualTo("a.txt=a.txt"));
    }

    [Test]
    public void Placeholder_names_are_case_insensitive()
    {
        Assert.That(Render("{FullPath}/{FILENAME}", @"C:\a.txt"), Is.EqualTo(@"C:\a.txt/a.txt"));
    }

    [Test]
    public void Doubled_braces_are_literal_braces()
    {
        Assert.That(Render("{{{filename}}}", @"C:\a.txt"), Is.EqualTo("{a.txt}"));
        Assert.That(Render("{{fullpath}}", @"C:\a.txt"), Is.EqualTo("{fullpath}"));
    }

    [Test]
    public void Backslash_t_and_n_become_a_tab_and_a_newline()
    {
        Assert.That(Render(@"{filename}\t{directory}", @"C:\data\a.txt"), Is.EqualTo("a.txt\tC:\\data"));
        Assert.That(Render(@"{filename}\n", @"C:\a.txt"), Is.EqualTo("a.txt\n"));
    }

    [Test]
    public void Other_backslashes_stay_literal()
    {
        Assert.That(Render(@"{directory}\{filename}", @"C:\data\a.txt"), Is.EqualTo(@"C:\data\a.txt"));
    }

    [Test]
    public void Literal_only_template_renders_unchanged()
    {
        Assert.That(Render("match", @"C:\a.txt"), Is.EqualTo("match"));
    }

    [Test]
    public void Unknown_placeholder_throws_and_lists_the_valid_names()
    {
        var ex = Assert.Throws<FormatException>(() => MatchTemplate.Parse("{owner}"));

        Assert.That(ex!.Message, Does.Contain("owner"));
        Assert.That(ex.Message, Does.Contain("{catalog}"));
        Assert.That(ex.Message, Does.Contain("{fullpath}"));
        Assert.That(ex.Message, Does.Contain("{filename}"));
        Assert.That(ex.Message, Does.Contain("{directory}"));
        Assert.That(ex.Message, Does.Contain("{size}"));
        Assert.That(ex.Message, Does.Contain("{modified}"));
    }

    // ---- {size} and {modified} ----

    private static readonly DateTime Stamp = new(2024, 3, 7, 14, 5, 9);

    private static string RenderWith(string template, long size = 0, DateTime modified = default) =>
        MatchTemplate.Parse(template).Render(Catalog, @"C:\a.txt", size, modified);

    [TestCase(0L, "0")]
    [TestCase(1234567L, "1234567")]
    [TestCase(long.MaxValue, "9223372036854775807")]
    public void Size_placeholder_is_the_size_in_bytes(long size, string expected)
    {
        Assert.That(RenderWith("{size}", size), Is.EqualTo(expected));
    }

    [Test]
    public void Modified_placeholder_without_a_format_is_year_month_day_hours_minutes_seconds()
    {
        Assert.That(RenderWith("{modified}", modified: Stamp), Is.EqualTo("2024-03-07 14:05:09"));
    }

    [TestCase("yyyy-MM-dd", "2024-03-07")]
    [TestCase("yyyy", "2024")]
    [TestCase("MM", "03")]
    [TestCase("dd", "07")]
    [TestCase("HH", "14")]
    [TestCase("mm", "05")]
    [TestCase("ss", "09")]
    [TestCase("HH:mm", "14:05")]
    [TestCase("yyyyMMdd_HHmmss", "20240307_140509")]
    [TestCase("dd/MM/yyyy", "07/03/2024")]
    [TestCase("yyyy-MM-ddTHH:mm", "2024-03-07T14:05")]
    public void Modified_format_tokens_are_replaced_and_other_characters_are_literal(string format, string expected)
    {
        Assert.That(RenderWith("{modified:" + format + "}", modified: Stamp), Is.EqualTo(expected));
    }

    [Test]
    public void Modified_tokens_are_case_sensitive_so_MM_is_month_and_mm_is_minutes()
    {
        Assert.That(RenderWith("{modified:MM mm}", modified: Stamp), Is.EqualTo("03 05"));
    }

    [Test]
    public void Modified_pads_small_years_and_uses_24_hour_time()
    {
        var early = new DateTime(987, 1, 2, 0, 0, 0);
        var evening = new DateTime(2024, 12, 31, 23, 59, 58);

        Assert.That(RenderWith("{modified:yyyy-MM-dd HH:mm:ss}", modified: early), Is.EqualTo("0987-01-02 00:00:00"));
        Assert.That(RenderWith("{modified:yyyy-MM-dd HH:mm:ss}", modified: evening), Is.EqualTo("2024-12-31 23:59:58"));
    }

    [Test]
    public void Modified_name_and_format_can_be_mixed_with_other_placeholders()
    {
        Assert.That(RenderWith("{filename} {size} {modified:yyyy-MM-dd}", 42, Stamp), Is.EqualTo("a.txt 42 2024-03-07"));
    }

    [Test]
    public void Modified_placeholder_name_is_case_insensitive_but_the_format_is_not()
    {
        Assert.That(RenderWith("{MODIFIED:yyyy-MM-dd}", modified: Stamp), Is.EqualTo("2024-03-07"));
    }

    [TestCase("{size:N0}")]
    [TestCase("{fullpath:x}")]
    [TestCase("{catalog:x}")]
    public void A_format_on_a_placeholder_that_takes_none_throws(string template)
    {
        var ex = Assert.Throws<FormatException>(() => MatchTemplate.Parse(template));

        Assert.That(ex!.Message, Does.Contain("format"));
    }

    [Test]
    public void An_empty_modified_format_throws()
    {
        Assert.Throws<FormatException>(() => MatchTemplate.Parse("{modified:}"));
    }

    [TestCase("{fullpath")]
    [TestCase("fullpath}")]
    [TestCase("{")]
    [TestCase("{}")]
    public void Unbalanced_or_empty_braces_throw(string template)
    {
        Assert.Throws<FormatException>(() => MatchTemplate.Parse(template));
    }

    [Test]
    public void A_syntax_error_says_where_it_is()
    {
        var stray = Assert.Throws<FormatException>(() => MatchTemplate.Parse("{filename} } {fullpath}"));
        var unclosed = Assert.Throws<FormatException>(() => MatchTemplate.Parse("{filename} {fullpath"));

        Assert.That(stray!.Message, Does.Contain("column 12"));
        Assert.That(unclosed!.Message, Does.Contain("end of input"));
    }

    [Test]
    public void Null_or_empty_template_throws()
    {
        Assert.Catch<ArgumentException>(() => MatchTemplate.Parse(null));
        Assert.Catch<ArgumentException>(() => MatchTemplate.Parse(""));
    }
}
