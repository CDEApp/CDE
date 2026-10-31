using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NUnit.Framework;
using Serilog.Core;

namespace cdeLibTest;

/// <summary>
/// <c>cde everything</c> run as the real program (the cde.dll beside the tests) over a catalog in its working
/// directory: the .cdex path, the legacy .cde path, the exit code and what goes to stdout and stderr.
/// </summary>
[TestFixture]
public class EverythingCommandProcessTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-everything-cmd").FullName;
        File.Copy(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), Path.Combine(_dir, "appsettings.json"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    // data/
    //   report.txt          10 bytes
    //   notes.md            5000 bytes
    //   reports/            (a folder)
    //     inner.txt
    //   docs/
    //     report-2024.txt   2000 bytes
    private RootEntry Scan()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "reports"));
        Directory.CreateDirectory(Path.Combine(data, "docs"));
        File.WriteAllBytes(Path.Combine(data, "report.txt"), new byte[10]);
        File.WriteAllBytes(Path.Combine(data, "notes.md"), new byte[5000]);
        File.WriteAllBytes(Path.Combine(data, "reports", "inner.txt"), new byte[1]);
        File.WriteAllBytes(Path.Combine(data, "docs", "report-2024.txt"), new byte[2000]);
        var root = new RootEntry();
        root.PopulateRoot(data);
        root.SortAllChildrenByPath();
        root.SetSummaryFields();
        return root;
    }

    private void WriteCdex() => ColumnarFormat.Write(Scan(), Path.Combine(_dir, "one.cdex"));

    private async Task WriteCde()
    {
        var root = Scan();
        root.ActualFileName = Path.Combine(_dir, "one.cde");
        using var repository = new CatalogRepository(Logger.None);
        await repository.Save(root);
    }

    private (int Exit, string[] Out, string Err) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "cde.dll"));
        foreach (var a in args) start.ArgumentList.Add(a);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.That(process.WaitForExit(60_000), Is.True, "cde did not finish");
        var lines = stdout.Result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).OrderBy(l => l, StringComparer.Ordinal).ToArray();
        return (process.ExitCode, lines, stderr.Result);
    }

    private static string[] Names(string[] paths) =>
        paths.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Test]
    public void Cdex_catalog_the_query_syntax_the_path_modifier_and_the_kind_filters()
    {
        WriteCdex();

        Assert.That(Names(Run("everything", "rep 2024").Out), Is.EqualTo(new[] { "report-2024.txt" }));
        Assert.That(Names(Run("everything", "path:docs report").Out), Is.EqualTo(new[] { "report-2024.txt" }));
        Assert.That(Names(Run("everything", "folder:report").Out), Is.EqualTo(new[] { "reports" }));
        Assert.That(Names(Run("everything", "file: report").Out), Is.EqualTo(new[] { "report-2024.txt", "report.txt" }));
        var all = Run("everything", "<file: .md> | <folder: docs>");
        Assert.That(all.Exit, Is.EqualTo(0));
        Assert.That(Names(all.Out), Is.EqualTo(new[] { "docs", "notes.md" }));
    }

    [Test]
    public async Task Legacy_cde_catalog_works_too()
    {
        await WriteCde();

        var result = Run("everything", "folder:report");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(Names(result.Out), Is.EqualTo(new[] { "reports" }));
        Assert.That(Names(Run("everything", "path:docs report").Out), Is.EqualTo(new[] { "report-2024.txt" }));
    }

    [Test]
    public void The_query_comes_first_then_filter_then_template()
    {
        WriteCdex();

        var result = Run("everything", "file: report", "--filter", "{size:>1KB}", "--template", "{filename}={size:B}");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Is.EqualTo(new[] { "report-2024.txt=2000 B" }));
    }

    [TestCase("a ||", 3)]
    [TestCase("<a", 0)]
    [TestCase("a path:file:", 2)]
    public void A_malformed_query_exits_1_with_the_message_and_position_and_prints_no_results(string query, int position)
    {
        WriteCdex();

        var result = Run("everything", query);

        Assert.That(result.Exit, Is.EqualTo(1));
        Assert.That(result.Err, Does.StartWith("error: "));
        Assert.That(result.Err, Does.Contain($"(at position {position})"));
        Assert.That(result.Out, Is.Empty);
    }

    [Test]
    public void A_malformed_query_fails_the_same_way_with_only_a_legacy_catalog()
    {
        WriteCde().GetAwaiter().GetResult();

        var result = Run("everything", "a ||");

        Assert.That(result.Exit, Is.EqualTo(1));
        Assert.That(result.Err, Does.Contain("(at position 3)"));
        Assert.That(result.Out, Is.Empty);
    }

    [Test]
    public void An_empty_query_lists_every_entry()
    {
        WriteCdex();

        var result = Run("everything", "");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(Names(result.Out), Is.EqualTo(new[] { "docs", "inner.txt", "notes.md", "report-2024.txt", "report.txt", "reports" }));
    }

    [Test]
    public void Find_is_unchanged_and_reads_the_pattern_as_a_plain_substring()
    {
        WriteCdex();

        Assert.That(Run("find", "rep 2024").Out, Is.Empty);
        Assert.That(Names(Run("find", "report").Out), Is.EqualTo(new[] { "report-2024.txt", "report.txt", "reports" }));
    }
}
