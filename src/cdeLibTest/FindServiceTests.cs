using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// <see cref="FindService"/>: each search mode (name / path, plain / regex) over loaded catalogs and over
/// memory-mapped columnar ones, with the file and folder filters. Matches are written to the console, so the output
/// is captured. Console.Out is process-wide, so these tests do not run in parallel.
/// </summary>
[TestFixture]
[NonParallelizable]
public class FindServiceTests
{
    private string _dir;
    private TextWriter _originalOut;
    private StringWriter _captured;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-find-service").FullName;
        _originalOut = Console.Out;
        _captured = new StringWriter();
        Console.SetOut(_captured);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    // data/
    //   report.txt
    //   notes.md
    //   reports/            (a folder whose name matches "report")
    //     inner.txt
    //   docs/
    //     report-2024.txt
    private RootEntry Scan()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "reports"));
        Directory.CreateDirectory(Path.Combine(data, "docs"));
        File.WriteAllText(Path.Combine(data, "report.txt"), "a");
        File.WriteAllText(Path.Combine(data, "notes.md"), "b");
        File.WriteAllText(Path.Combine(data, "reports", "inner.txt"), "c");
        File.WriteAllText(Path.Combine(data, "docs", "report-2024.txt"), "d");
        var root = new RootEntry();
        root.PopulateRoot(data);
        root.SortAllChildrenByPath();
        root.SetSummaryFields();
        return root;
    }

    private ColumnarCatalogReader Columnar(string name = "one.cdex")
    {
        var cdex = Path.Combine(_dir, name);
        var root = Scan();
        ColumnarFormat.Write(root, cdex);
        return new ColumnarCatalogReader(cdex);
    }

    private List<string> Matches() =>
        _captured.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).OrderBy(l => l, StringComparer.Ordinal).ToList();

    private static string[] Names(IEnumerable<string> paths) =>
        paths.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // ---- defaults ----

    [Test]
    public void New_service_includes_files_and_folders()
    {
        var service = new FindService();

        Assert.That(service.IncludeFiles, Is.True);
        Assert.That(service.IncludeFolders, Is.True);
    }

    // ---- tree path: Find(pattern, param, roots) ----

    [Test]
    public void Find_Name_matches_files_and_folders_by_name_only()
    {
        new FindService().Find("report", FindService.ParamFind, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt", "reports" }));
    }

    [Test]
    public void Find_Name_does_not_match_on_the_folder_a_file_is_in()
    {
        new FindService().Find("docs", FindService.ParamFind, [Scan()]);

        // only the "docs" folder itself: report-2024.txt lives in it, but its own name has no "docs"
        Assert.That(Names(Matches()), Is.EqualTo(new[] { "docs" }));
    }

    [Test]
    public void FindPath_matches_against_the_whole_path()
    {
        new FindService().Find("docs", FindService.ParamFindPath, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "docs", "report-2024.txt" }));
    }

    [Test]
    public void Grep_treats_the_pattern_as_a_regex_on_names()
    {
        new FindService().Find(@"^report\.txt$", FindService.ParamGrep, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report.txt" }));
    }

    [Test]
    public void GrepPath_treats_the_pattern_as_a_regex_on_paths()
    {
        new FindService().Find(@"docs.report-\d+\.txt$", FindService.ParamGrepPath, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt" }));
    }

    [Test]
    public void Find_FilesOnly_leaves_out_folders()
    {
        new FindService { IncludeFolders = false }.Find("report", FindService.ParamFind, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt" }));
    }

    [Test]
    public void Find_FoldersOnly_leaves_out_files()
    {
        new FindService { IncludeFiles = false }.Find("report", FindService.ParamFind, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "reports" }));
    }

    [Test]
    public void Find_NoMatch_prints_nothing()
    {
        new FindService().Find("zzz-nothing", FindService.ParamFind, [Scan()]);

        Assert.That(Matches(), Is.Empty);
    }

    [Test]
    public void Find_releases_the_trees_it_was_given()
    {
        var roots = new List<RootEntry> { Scan() };

        new FindService().Find("report", FindService.ParamFind, roots);

        Assert.That(roots, Is.EqualTo(new RootEntry[] { null }), "the list is cleared so the trees can be collected");
    }

    [Test]
    public void Find_skips_null_catalogs_and_searches_the_rest()
    {
        new FindService().Find("report", FindService.ParamFind, [null, Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt", "reports" }));
    }

    [Test]
    public void Find_with_no_catalogs_prints_nothing()
    {
        Assert.DoesNotThrow(() => new FindService().Find("x", FindService.ParamFind, new List<RootEntry>()));
        Assert.That(Matches(), Is.Empty);
    }

    [Test]
    public void Find_searches_every_catalog()
    {
        new FindService().Find("notes", FindService.ParamFind, [Scan(), Scan()]);

        Assert.That(Matches(), Has.Count.EqualTo(2));
    }

    [Test]
    public void Find_with_flags_matches_the_param_form()
    {
        var service = new FindService();
        service.Find("docs", regexMode: false, includePath: true, [Scan()]);
        var viaFlags = Matches();
        _captured.GetStringBuilder().Clear();

        service.Find("docs", FindService.ParamFindPath, [Scan()]);

        Assert.That(Matches(), Is.EqualTo(viaFlags));
    }

    // An unknown parameter is neither regex nor path: a plain name search.
    [Test]
    public void Find_unknown_param_is_a_plain_name_search()
    {
        new FindService().Find("report", "--other", [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt", "reports" }));
    }

    // ---- async wrappers ----

    [Test]
    public async Task FindAsync_Param_searches_and_completes()
    {
        await new FindService().FindAsync("docs", FindService.ParamFindPath, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "docs", "report-2024.txt" }));
    }

    [Test]
    public async Task FindAsync_Flags_searches_and_completes()
    {
        await new FindService().FindAsync(@"^notes", regexMode: true, includePath: false, [Scan()]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "notes.md" }));
    }

    // ---- columnar path ----

    [Test]
    public void FindColumnar_Name_matches_files_and_folders()
    {
        using var reader = Columnar();

        new FindService().FindColumnar("report", FindService.ParamFind, [reader]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt", "reports" }));
    }

    [Test]
    public void FindColumnar_GrepPath_uses_a_regex_on_paths()
    {
        using var reader = Columnar();

        new FindService().FindColumnar(@"docs.report-\d+", FindService.ParamGrepPath, [reader]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt" }));
    }

    [Test]
    public void FindColumnar_FindPath_matches_against_the_whole_path()
    {
        using var reader = Columnar();

        new FindService().FindColumnar("docs", FindService.ParamFindPath, [reader]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "docs", "report-2024.txt" }));
    }

    [Test]
    public void FindColumnar_filters_apply()
    {
        using var reader = Columnar();

        new FindService { IncludeFolders = false }.FindColumnar("report", FindService.ParamFind, [reader]);

        Assert.That(Names(Matches()), Is.EqualTo(new[] { "report-2024.txt", "report.txt" }));
    }

    [Test]
    public void FindColumnar_skips_null_readers_and_searches_every_other_one()
    {
        using var first = Columnar("one.cdex");
        using var second = Columnar("two.cdex");

        new FindService().FindColumnar("notes", FindService.ParamFind, [first, null, second]);

        Assert.That(Matches(), Has.Count.EqualTo(2));
    }

    [Test]
    public void FindColumnar_gives_the_same_matches_as_the_tree_search()
    {
        new FindService().Find("report", FindService.ParamGrepPath, [Scan()]);
        var fromTree = Matches();
        _captured.GetStringBuilder().Clear();
        using var reader = Columnar();

        new FindService().FindColumnar("report", FindService.ParamGrepPath, [reader]);

        Assert.That(Matches(), Is.EqualTo(fromTree));
    }

    // ---- --template ----

    [Test]
    public void New_service_has_no_template()
    {
        Assert.That(new FindService().Template, Is.Null);
    }

    [Test]
    public void Default_output_is_a_leading_space_then_the_full_path()
    {
        using var reader = Columnar();

        new FindService().FindColumnar("report-2024", FindService.ParamFind, [reader]);

        var expected = " " + Path.Combine(_dir, "data", "docs", "report-2024.txt") + Environment.NewLine;
        Assert.That(_captured.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void FindColumnar_renders_each_match_with_the_template()
    {
        using var reader = Columnar("one.cdex");

        new FindService { Template = MatchTemplate.Parse("{catalog}|{directory}|{filename}") }
            .FindColumnar("report-2024", FindService.ParamFind, [reader]);

        var expected = $"one|{Path.Combine(_dir, "data", "docs")}|report-2024.txt{Environment.NewLine}";
        Assert.That(_captured.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void FindColumnar_template_names_the_catalog_each_match_came_from()
    {
        using var first = Columnar("first.cdex");
        using var second = Columnar("second.cdex");

        new FindService { Template = MatchTemplate.Parse("{catalog}") }
            .FindColumnar("notes", FindService.ParamFind, [first, second]);

        Assert.That(Matches(), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void Find_renders_each_match_with_the_template_on_the_tree_path()
    {
        var root = Scan();
        root.ActualFileName = Path.Combine(_dir, "tree.cde");

        new FindService { Template = MatchTemplate.Parse("{catalog}|{fullpath}") }
            .Find("report-2024", FindService.ParamFind, [root]);

        var expected = $"tree|{Path.Combine(_dir, "data", "docs", "report-2024.txt")}{Environment.NewLine}";
        Assert.That(_captured.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Tree_path_falls_back_to_the_default_file_name_for_the_catalog()
    {
        var root = Scan();
        root.ActualFileName = null;
        root.DefaultFileName = "generated.cde";

        new FindService { Template = MatchTemplate.Parse("{catalog}") }
            .Find("notes", FindService.ParamFind, [root]);

        Assert.That(Matches(), Is.EqualTo(new[] { "generated" }));
    }

    [Test]
    public void FindColumnar_template_renders_size_and_modified()
    {
        var file = Path.Combine(_dir, "data", "docs", "report-2024.txt");
        using var reader = Columnar();
        var expected = $"{new FileInfo(file).Length}|{File.GetLastWriteTime(file):yyyy-MM-dd HH:mm:ss}";

        new FindService { Template = MatchTemplate.Parse("{size}|{modified:yyyy-MM-dd HH:mm:ss}") }
            .FindColumnar("report-2024", FindService.ParamFind, [reader]);

        Assert.That(Matches(), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Find_template_renders_size_and_modified_on_the_tree_path()
    {
        var root = Scan();
        var file =Path.Combine(_dir, "data", "docs", "report-2024.txt");
        var expected = $"{new FileInfo(file).Length}|{File.GetLastWriteTime(file):yyyy-MM-dd HH:mm:ss}";

        new FindService { Template = MatchTemplate.Parse("{size}|{modified:yyyy-MM-dd HH:mm:ss}") }
            .Find("report-2024", FindService.ParamFind, [root]);

        Assert.That(Matches(), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Template_works_with_the_greppath_verb()
    {
        using var reader = Columnar();

        new FindService { Template = MatchTemplate.Parse("{filename}") }
            .FindColumnar(@"docs.report-\d+\.txt$", FindService.ParamGrepPath, [reader]);

        Assert.That(Matches(), Is.EqualTo(new[] { "report-2024.txt" }));
    }
}
