using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

namespace cdeLibTest.Output;

/// <summary>
/// <c>cde find</c>, <c>dupes</c> and <c>hash</c> run as the real program (the cde.dll beside the tests) over
/// catalogs in a working directory: what goes to stdout and stderr by default and with <c>--json</c> / <c>--csv</c> (#173).
/// The default output is pinned here byte for byte, so the machine formats can't change it.
/// </summary>
[TestFixture]
public class MachineOutputProcessTests
{
    private string _dir;
    private string _configDir;

    [SetUp]
    public void SetUp()
    {
        _configDir = null;
        _dir = Directory.CreateTempSubdirectory("cde-machine-output").FullName;
        File.Copy(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), Path.Combine(_dir, "appsettings.json"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    private static DirEntry File_(string name, long size, int? hash = null)
    {
        var d = new DirEntry(false) { Path = name, Size = size, Modified = new DateTime(2024, 3, 5, 6, 7, 8, DateTimeKind.Utc) };
        if (hash.HasValue) d.SetHash(hash.Value);
        return d;
    }

    // data/ (C:\data)
    //   a/ x.txt (100, hash 1)   y.txt (7)
    //   b/ x.txt (100, hash 1)   "q,u""o\nte.txt" (100, hash 2)   ünï.txt (7)
    // The duplicate set is the two x.txt files; the awkward names are the ones a text format has to get right.
    private string WriteCdex(string fileName = "one.cdex")
    {
        var root = new RootEntry { Path = @"C:\data" };
        var a = new DirEntry(true) { Path = "a" };
        var b = new DirEntry(true) { Path = "b" };
        a.AddChild(File_("x.txt", 100, 1));
        a.AddChild(File_("y.txt", 7));
        b.AddChild(File_("x.txt", 100, 1));
        b.AddChild(File_("q,u\"o\nte.txt", 100, 2));
        b.AddChild(File_("ünï.txt", 7));
        root.AddChild(a);
        root.AddChild(b);
        root.SetInMemoryFields();
        var path = Path.Combine(_dir, fileName);
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    private (int Exit, string Out, string Err) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new System.Text.UTF8Encoding(false),
            StandardErrorEncoding = new System.Text.UTF8Encoding(false),
            UseShellExecute = false,
        };
        if (_configDir != null) start.Environment[AppSettingsLocator.ConfigDirVariable] = _configDir;
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "cde.dll"));
        foreach (var a in args) start.ArgumentList.Add(a);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.That(process.WaitForExit(60_000), Is.True, "cde did not finish");
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    private static string Lines(params string[] lines) => string.Concat(lines.Select(l => l + Environment.NewLine));

    // ---- default output is unchanged ----

    [Test]
    public void Default_find_writes_a_leading_space_then_the_full_path_per_match()
    {
        var cdex = WriteCdex();
        using var reader = new ColumnarCatalogReader(cdex);
        var expected = Enumerable.Range(1, reader.Count - 1)
            .Where(i => !reader.IsDirectory(i) && reader.NameOf(i).Contains("x.txt"))
            .Select(i => " " + reader.FullPath(i)).OrderBy(l => l, StringComparer.Ordinal).ToArray();

        var result = Run("find", "x.txt");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        var lines = result.Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.OrderBy(l => l, StringComparer.Ordinal), Is.EqualTo(expected));
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(result.Out, Does.EndWith(Environment.NewLine));
    }

    [Test]
    public void Default_dupes_writes_the_path_of_each_file_in_each_set()
    {
        var cdex = WriteCdex();
        using var reader = new ColumnarCatalogReader(cdex);
        var paths = Enumerable.Range(1, reader.Count - 1)
            .Where(i => !reader.IsDirectory(i) && reader.NameOf(i) == "x.txt").Select(reader.FullPath).ToArray();

        var result = Run("dupes");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Is.EqualTo(Lines(paths)));
    }

    // ---- machine formats ----

    private sealed record Row(string Path, string Name, long Size, bool Directory);

    private static List<JsonElement> JsonLines(string stdout) =>
        stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    // Every entry of the catalog the way the reader (the independent source of truth) reports it, for names containing text.
    private static Row[] Expected(string cdex, string containing)
    {
        using var reader = new ColumnarCatalogReader(cdex);
        return Enumerable.Range(1, reader.Count - 1).Where(i => reader.NameOf(i).Contains(containing))
            .Select(i => new Row(reader.FullPath(i), reader.NameOf(i), reader.SizeOf(i), reader.IsDirectory(i)))
            .OrderBy(r => r.Path, StringComparer.Ordinal).ToArray();
    }

    private static Row[] Ordered(IEnumerable<Row> rows) => rows.OrderBy(r => r.Path, StringComparer.Ordinal).ToArray();

    private static Row[] FromJson(string stdout) => Ordered(JsonLines(stdout).Select(o =>
        new Row(o.GetProperty("path").GetString(), o.GetProperty("name").GetString(), o.GetProperty("size").GetInt64(),
            o.GetProperty("directory").GetBoolean())));

    private static Row[] FromCsv(List<string[]> rows)
    {
        var header = rows[0];
        string Col(string[] r, string name) => r[Array.IndexOf(header, name)];
        return Ordered(rows.Skip(1).Select(r => new Row(Col(r, "path"), Col(r, "name"), long.Parse(Col(r, "size")),
            bool.Parse(Col(r, "directory")))));
    }

    [Test]
    public void Find_json_is_one_object_per_match_and_reads_back_to_the_catalog_entries()
    {
        var cdex = WriteCdex();

        var result = Run("find", ".txt", "--json");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(FromJson(result.Out), Is.EqualTo(Expected(cdex, ".txt")));
        Assert.That(FromJson(result.Out).Select(r => r.Name), Does.Contain("q,u\"o\nte.txt").And.Contain("ünï.txt"));
        var first = JsonLines(result.Out)[0];
        Assert.That(first.GetProperty("modified").GetString(), Is.EqualTo("2024-03-05T06:07:08Z"));
        Assert.That(first.GetProperty("catalog").GetString(), Does.EndWith("one.cdex"));
        Assert.That(result.Err, Is.Empty, "nothing but the data was written, and nothing went wrong");
    }

    [Test]
    public void Find_csv_has_a_header_and_reads_back_to_the_catalog_entries()
    {
        var cdex = WriteCdex();

        var result = Run("find", ".txt", "--csv");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        var rows = CsvText.Parse(result.Out);
        Assert.That(rows[0], Is.EqualTo(new[] { "catalog", "path", "name", "size", "modified", "directory" }));
        Assert.That(FromCsv(rows), Is.EqualTo(Expected(cdex, ".txt")));
    }

    [Test]
    public void Find_flags_a_folder_as_a_directory()
    {
        WriteCdex();

        var result = Run("find", "a", "--json");

        Assert.That(FromJson(result.Out).Select(r => (r.Name, r.Directory)), Is.EqualTo(new[] { ("a", true) }));
    }

    [Test]
    public void A_search_with_no_match_writes_the_csv_header_alone_and_nothing_for_json()
    {
        WriteCdex();

        Assert.That(Run("find", "zzz", "--json").Out, Is.Empty);
        Assert.That(CsvText.Parse(Run("find", "zzz", "--csv").Out), Has.Count.EqualTo(1));
    }

    [TestCase("find")]
    [TestCase("grep")]
    [TestCase("findpath")]
    [TestCase("greppath")]
    [TestCase("everything")]
    public void Every_search_verb_takes_the_machine_formats(string verb)
    {
        var cdex = WriteCdex();

        var result = Run(verb, "x.txt", "--json");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(FromJson(result.Out), Is.EqualTo(Expected(cdex, "x.txt")));
    }

    [Test]
    public void Logs_go_to_stderr_so_stdout_stays_data_only()
    {
        WriteCdex();

        // Turn logging up with a settings file of its own (the config folder is looked in first).
        var config = Directory.CreateDirectory(Path.Combine(_dir, "config")).FullName;
        File.WriteAllText(Path.Combine(config, "appsettings.json"),
            """{ "Serilog": { "Using": [ "Serilog.Sinks.Console" ], "MinimumLevel": "Information", "WriteTo": [ { "Name": "Console" } ] } }""");
        _configDir = config;

        var result = Run("find", ".txt", "--json");

        Assert.That(result.Err, Does.Contain("Search Execution Time"));
        Assert.That(JsonLines(result.Out), Has.Count.EqualTo(5), "every stdout line is a JSON object");
    }

    [Test]
    public void Dupes_json_gives_each_file_of_a_set_the_set_s_group_and_its_hash()
    {
        var cdex = WriteCdex();
        var expected = Expected(cdex, "x.txt");

        var result = Run("dupes", "--json");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        var rows = JsonLines(result.Out);
        Assert.That(Ordered(rows.Select(o => new Row(o.GetProperty("path").GetString(), o.GetProperty("name").GetString(),
            o.GetProperty("size").GetInt64(), false))), Is.EqualTo(expected.Select(r => r with { Directory = false })));
        Assert.That(rows.Select(o => o.GetProperty("group").GetInt64()).Distinct().Count(), Is.EqualTo(1));
        Assert.That(rows.Select(o => o.GetProperty("hash").GetString()).Distinct().Single(), Has.Length.EqualTo(32));
        Assert.That(result.Err, Is.Empty);
    }

    [Test]
    public void Dupes_csv_has_the_same_fields_as_json()
    {
        WriteCdex();

        var json = JsonLines(Run("dupes", "--json").Out);
        var rows = CsvText.Parse(Run("dupes", "--csv").Out);

        Assert.That(rows[0], Is.EqualTo(new[] { "group", "catalog", "path", "name", "size", "modified", "hash" }));
        Assert.That(rows.Skip(1).Select(r => r[2]), Is.EqualTo(json.Select(o => o.GetProperty("path").GetString())));
        Assert.That(rows.Skip(1).Select(r => r[6]), Is.EqualTo(json.Select(o => o.GetProperty("hash").GetString())));
    }

    [TestCase("find", "x")]
    [TestCase("dupes")]
    [TestCase("hash")]
    public void Json_and_csv_together_are_refused_before_anything_runs(params string[] verb)
    {
        WriteCdex();

        var result = Run([.. verb, "--json", "--csv"]);

        Assert.That(result.Exit, Is.EqualTo(1));
        Assert.That(result.Err, Does.StartWith("error: ").And.Contain("--json").And.Contain("--csv"));
        Assert.That(result.Out, Is.Empty);
    }

    [Test]
    public void A_template_cannot_be_combined_with_a_machine_format()
    {
        WriteCdex();

        var result = Run("find", "x", "--csv", "--template", "{fullpath}");

        Assert.That(result.Exit, Is.EqualTo(1));
        Assert.That(result.Err, Does.StartWith("error: ").And.Contain("--template"));
        Assert.That(result.Out, Is.Empty);
    }

    // data/ (real files, so they can be hashed)
    //   a,b.txt   hello       ünï.txt   hello       solo.txt   abc
    private string WriteScannedCdex()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a,b.txt"), "hello");
        File.WriteAllText(Path.Combine(data, "ünï.txt"), "hello");
        File.WriteAllText(Path.Combine(data, "solo.txt"), "abc");
        var root = new RootEntry();
        root.PopulateRoot(data);
        root.SortAllChildrenByPath();
        root.SetSummaryFields();
        var cdex = Path.Combine(_dir, "scanned.cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    [Test]
    public void Hash_json_reports_each_file_it_hashed_with_the_hash_the_catalog_ends_up_holding()
    {
        var cdex = WriteScannedCdex();

        var result = Run("hash", "--json");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        var rows = JsonLines(result.Out);
        Assert.That(rows.Select(o => o.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "a,b.txt", "ünï.txt" }));
        Assert.That(rows.Select(o => o.GetProperty("size").GetInt64()), Is.All.EqualTo(5));
        Assert.That(rows.Select(o => o.GetProperty("partial").GetBoolean()), Is.All.False, "a small file is hashed in full");
        Assert.That(rows.Select(o => o.GetProperty("hash").GetString()).Distinct().Single(), Has.Length.EqualTo(32));
        Assert.That(result.Err, Does.Contain("Checking that the drive"), "progress and status go to stderr");

        using var reader = new ColumnarCatalogReader(cdex);
        foreach (var o in rows)
        {
            var i = Enumerable.Range(1, reader.Count - 1).Single(k => reader.FullPath(k) == o.GetProperty("path").GetString());
            var hash = reader.HashOf(i);
            Assert.That(o.GetProperty("hash").GetString(), Is.EqualTo(hash.HashA.ToString("x16") + hash.HashB.ToString("x16")));
        }
    }

    [Test]
    public void Hash_csv_has_a_header_and_the_same_records()
    {
        WriteScannedCdex();

        var result = Run("hash", "--csv");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        var rows = CsvText.Parse(result.Out);
        Assert.That(rows[0], Is.EqualTo(new[] { "catalog", "path", "name", "size", "hash", "partial" }));
        Assert.That(rows.Skip(1).Select(r => r[2]), Is.EquivalentTo(new[] { "a,b.txt", "ünï.txt" }));
    }

    [Test]
    public void Default_hash_still_writes_its_status_line_to_stdout()
    {
        WriteScannedCdex();

        var result = Run("hash");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Contain("Checking that the drive or share of each of 1 catalog(s) can be reached..."));
        Assert.That(result.Out, Does.Not.Contain("{"));
    }
}
