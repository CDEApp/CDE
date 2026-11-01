using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure;

/// <summary>
/// Where appsettings.json is found (#158): the config folder, then the current directory, then the executable's
/// folder; the first file found is used on its own; when there is none a default is written to the config folder.
/// Every folder and the environment are given to the locator, so nothing here touches the real user config folder.
/// </summary>
[TestFixture]
public class AppSettingsLocatorTests
{
    private string _root;
    private string _appData;
    private string _cwd;
    private string _exe;
    private readonly Dictionary<string, string> _env = new();

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("cde-locator").FullName;
        _appData = Path.Combine(_root, "appdata");
        _cwd = Path.Combine(_root, "cwd");
        _exe = Path.Combine(_root, "exe");
        Directory.CreateDirectory(_appData);
        Directory.CreateDirectory(_cwd);
        Directory.CreateDirectory(_exe);
        _env.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { /* ignore */ }
    }

    private AppSettingsLocator Locator() =>
        new(name => _env.GetValueOrDefault(name), _appData, _cwd, _exe);

    private static string Put(string folder, string text)
    {
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, AppSettingsLocator.FileName);
        File.WriteAllText(file, text);
        return file;
    }

    private string DefaultConfigFolder => Path.Combine(_appData, "cde");

    // ---- the config folder ----

    [Test]
    public void The_config_folder_is_cde_inside_the_application_data_folder()
    {
        Assert.That(Locator().ConfigFolder, Is.EqualTo(DefaultConfigFolder));
    }

    [Test]
    public void CDE_CONFIG_DIR_replaces_the_config_folder()
    {
        var custom = Path.Combine(_root, "custom");
        _env["CDE_CONFIG_DIR"] = custom;

        Assert.That(Locator().ConfigFolder, Is.EqualTo(custom));
    }

    [Test]
    public void An_empty_CDE_CONFIG_DIR_is_ignored()
    {
        _env["CDE_CONFIG_DIR"] = "  ";

        Assert.That(Locator().ConfigFolder, Is.EqualTo(DefaultConfigFolder));
    }

    // On a new Linux account or in a bare container there is no ~/.config yet, and .NET's default option answers with an
    // empty string for a folder that does not exist. The locator must create the folder, not give up.
    [Test]
    public void The_application_data_folder_need_not_exist()
    {
        _appData = Path.Combine(_root, "no", "such", "folder");

        var found = Locator().Locate();

        Assert.That(found.Source, Is.EqualTo(ConfigSource.Created));
        Assert.That(File.Exists(Path.Combine(_appData, "cde", AppSettingsLocator.FileName)), Is.True);
    }

    [Test]
    public void With_no_application_data_folder_and_no_override_there_is_no_config_folder_and_the_defaults_are_used()
    {
        _appData = "";

        var locator = Locator();
        var found = locator.Locate();

        Assert.That(locator.ConfigFolder, Is.Null);
        Assert.That(found.Source, Is.EqualTo(ConfigSource.BuiltIn));
        Assert.That(found.Error, Does.Contain("no user config folder"));
    }

    // ---- the lookup order ----

    [Test]
    public void The_config_folder_comes_first()
    {
        var inConfig = Put(DefaultConfigFolder, "{}");
        Put(_cwd, "{}");
        Put(_exe, "{}");

        var found = Locator().Locate();

        Assert.That(found.Source, Is.EqualTo(ConfigSource.ConfigFolder));
        Assert.That(found.Path, Is.EqualTo(inConfig));
    }

    [Test]
    public void The_current_directory_comes_before_the_executable_folder()
    {
        var inCwd = Put(_cwd, "{}");
        Put(_exe, "{}");

        var found = Locator().Locate();

        Assert.That(found.Source, Is.EqualTo(ConfigSource.CurrentDirectory));
        Assert.That(found.Path, Is.EqualTo(inCwd));
    }

    [Test]
    public void The_executable_folder_is_the_last_place()
    {
        var inExe = Put(_exe, "{}");

        var found = Locator().Locate();

        Assert.That(found.Source, Is.EqualTo(ConfigSource.ExecutableFolder));
        Assert.That(found.Path, Is.EqualTo(inExe));
    }

    [Test]
    public void CDE_CONFIG_DIR_is_the_first_place_looked_in()
    {
        var custom = Path.Combine(_root, "custom");
        _env["CDE_CONFIG_DIR"] = custom;
        var inCustom = Put(custom, "{}");
        Put(DefaultConfigFolder, "{}");
        Put(_cwd, "{}");

        Assert.That(Locator().Locate().Path, Is.EqualTo(inCustom));
    }

    [Test]
    public void The_first_file_found_is_used_on_its_own_with_no_merging()
    {
        Put(DefaultConfigFolder, """{ "AppConfig": { "Display": { "ProgressUpdateInterval": 111 } } }""");
        Put(_cwd, """{ "AppConfig": { "Hashing": { "FirstPassSizeInBytes": 222, "DegreesOfParallelism": 3 } } }""");
        Put(_exe, """{ "Extra": "from the executable folder" }""");

        var found = Locator().Locate();
        var config = new ConfigurationBuilder().Add(AppSettingsLocator.CreateSource(found)).Build();

        Assert.That(config["AppConfig:Display:ProgressUpdateInterval"], Is.EqualTo("111"));
        Assert.That(config["AppConfig:Hashing:FirstPassSizeInBytes"], Is.Null, "the current directory's file is not merged in");
        Assert.That(config["Extra"], Is.Null, "nor is the executable folder's");
    }

    // ---- nothing found: write the default ----

    [Test]
    public void When_there_is_no_file_the_default_is_written_to_the_config_folder()
    {
        var found = Locator().Locate();

        var expected = Path.Combine(DefaultConfigFolder, AppSettingsLocator.FileName);
        Assert.That(found.Source, Is.EqualTo(ConfigSource.Created));
        Assert.That(found.Path, Is.EqualTo(expected));
        Assert.That(File.ReadAllText(expected), Is.EqualTo(AppSettingsLocator.DefaultJson));
        Assert.That(found.Error, Is.Null);
    }

    [Test]
    public void The_config_folder_is_created_when_it_is_missing()
    {
        var custom = Path.Combine(_root, "a", "b", "custom");
        _env["CDE_CONFIG_DIR"] = custom;

        Locator().Locate();

        Assert.That(File.Exists(Path.Combine(custom, AppSettingsLocator.FileName)), Is.True);
    }

    [Test]
    public void A_second_run_finds_the_file_the_first_run_wrote_and_does_not_write_again()
    {
        var first = Locator().Locate();
        var written = File.GetLastWriteTimeUtc(first.Path);

        var second = Locator().Locate();

        Assert.That(second.Source, Is.EqualTo(ConfigSource.ConfigFolder));
        Assert.That(second.Path, Is.EqualTo(first.Path));
        Assert.That(File.GetLastWriteTimeUtc(second.Path), Is.EqualTo(written));
    }

    [Test]
    public void A_file_that_exists_is_never_overwritten()
    {
        var mine = Put(DefaultConfigFolder, """{ "mine": true }""");

        Locator().Locate();

        Assert.That(File.ReadAllText(mine), Is.EqualTo("""{ "mine": true }"""));
    }

    [Test]
    public void Nothing_is_written_when_a_file_is_found_in_the_current_directory_or_the_executable_folder()
    {
        Put(_cwd, "{}");
        Locator().Locate();
        Assert.That(Directory.Exists(DefaultConfigFolder), Is.False);

        File.Delete(Path.Combine(_cwd, AppSettingsLocator.FileName));
        Put(_exe, "{}");
        Locator().Locate();
        Assert.That(Directory.Exists(DefaultConfigFolder), Is.False);
    }

    [Test]
    public void When_the_write_fails_the_built_in_defaults_are_used_and_the_reason_is_kept()
    {
        // The config folder's parent is a file, so the folder cannot be created.
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "a file, not a folder");
        _env["CDE_CONFIG_DIR"] = Path.Combine(blocker, "cde");

        var found = Locator().Locate();

        Assert.That(found.Source, Is.EqualTo(ConfigSource.BuiltIn));
        Assert.That(found.Path, Is.Null);
        Assert.That(found.Error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Two_writers_racing_make_one_complete_file_and_neither_fails()
    {
        var results = new List<ConfigLocation>();
        using var gate = new System.Threading.ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            var locator = Locator();
            gate.Wait();
            var found = locator.Locate();
            lock (results) results.Add(found);
        })).ToArray();
        gate.Set();
        Task.WaitAll(tasks);

        var file = Path.Combine(DefaultConfigFolder, AppSettingsLocator.FileName);
        Assert.That(results, Has.Count.EqualTo(8));
        Assert.That(results.Select(r => r.Path).Distinct(), Is.EqualTo(new[] { file }));
        Assert.That(results.Count(r => r.Source == ConfigSource.Created), Is.GreaterThanOrEqualTo(1), "a writer created it");
        Assert.That(results.All(r => r.Source is ConfigSource.Created or ConfigSource.ConfigFolder), Is.True);
        Assert.That(File.ReadAllText(file), Is.EqualTo(AppSettingsLocator.DefaultJson));
        Assert.That(Directory.GetFiles(DefaultConfigFolder).Select(Path.GetFileName), Is.EqualTo(new[] { AppSettingsLocator.FileName }),
            "no temporary file is left behind");
    }

    // ---- the default text ----

    private static IConfigurationRoot Parse(string json) =>
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

    [Test]
    public void The_default_text_holds_the_documented_values()
    {
        var config = Parse(AppSettingsLocator.DefaultJson);
        var configuration = new Configuration(config);

        Assert.That(configuration.ProgressUpdateInterval, Is.EqualTo(5000));
        Assert.That(configuration.HashFirstPassSize, Is.EqualTo(65536));
        Assert.That(configuration.DegreesOfParallelism, Is.EqualTo(2));
        Assert.That(config["Cde:ConfigPath"], Is.EqualTo("."));
        Assert.That(config.GetSection("CustomCommands").GetChildren(), Is.Empty);
        Assert.That(config["Serilog:MinimumLevel"], Is.EqualTo("Warning"));
        Assert.That(config.GetSection("Serilog:WriteTo").GetChildren().Select(c => c["Name"]), Is.EqualTo(new[] { "Console" }));
    }

    [Test]
    public void The_default_text_has_no_Seq_sink()
    {
        // "ConsoleLogToSeq" is an existing setting name (it routes logging through Serilog); what must not be there is the sink.
        var sinks = Parse(AppSettingsLocator.DefaultJson).GetSection("Serilog:WriteTo").GetChildren().Select(c => c["Name"]);
        Assert.That(sinks, Does.Not.Contain("Seq"));
        Assert.That(AppSettingsLocator.DefaultJson, Does.Not.Contain("5341"));
        Assert.That(AppSettingsLocator.DefaultJson, Does.Not.Contain("serverUrl"));
    }

    [Test]
    public void The_built_in_defaults_and_the_written_file_are_the_same_text()
    {
        var builtIn = new ConfigurationBuilder()
            .Add(AppSettingsLocator.CreateSource(new ConfigLocation(ConfigSource.BuiltIn, null, "x"))).Build();
        var written = Locator().Locate();
        var fromFile = new ConfigurationBuilder().Add(AppSettingsLocator.CreateSource(written)).Build();

        var a = builtIn.AsEnumerable().OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}");
        var b = fromFile.AsEnumerable().OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}");
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void A_file_with_no_hashing_section_hashes_like_the_default()
    {
        // The class default used to be 1024 while the shipped file said 65536.
        var configuration = new Configuration(Parse("""{ "AppConfig": { "Display": { "ProgressUpdateInterval": 5000 } } }"""));

        Assert.That(configuration.HashFirstPassSize, Is.EqualTo(65536));
    }
}
