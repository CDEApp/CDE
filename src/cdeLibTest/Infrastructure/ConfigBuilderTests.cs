using System.IO;
using cde.Config;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure;

/// <summary>
/// How the cde command line builds its configuration (#158): from the file the locator finds, or from the built-in
/// defaults, with the command line on top. A missing file is no longer an error.
/// </summary>
[TestFixture]
public class ConfigBuilderTests
{
    private string _root;
    private string _configFolder;
    private string _cwd;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("cde-configbuilder").FullName;
        _configFolder = Path.Combine(_root, "config");
        _cwd = Path.Combine(_root, "cwd");
        Directory.CreateDirectory(_cwd);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { /* ignore */ }
    }

    private AppSettingsLocator Locator() =>
        new(name => name == AppSettingsLocator.ConfigDirVariable ? _configFolder : null,
            appDataFolder: Path.Combine(_root, "appdata"), currentDirectory: _cwd, executableFolder: Path.Combine(_root, "exe"));

    [Test]
    public void The_file_in_the_config_folder_is_the_one_read()
    {
        Directory.CreateDirectory(_configFolder);
        File.WriteAllText(Path.Combine(_configFolder, "appsettings.json"),
            """{ "AppConfig": { "Display": { "ProgressUpdateInterval": 111 } } }""");
        File.WriteAllText(Path.Combine(_cwd, "appsettings.json"),
            """{ "AppConfig": { "Display": { "ProgressUpdateInterval": 222 } } }""");

        var config = ConfigBuilder.Build([], Locator(), out var location);

        Assert.That(new Configuration(config).ProgressUpdateInterval, Is.EqualTo(111));
        Assert.That(location.Source, Is.EqualTo(ConfigSource.ConfigFolder));
    }

    [Test]
    public void With_no_file_anywhere_cde_runs_on_the_defaults_and_writes_them()
    {
        var config = ConfigBuilder.Build([], Locator(), out var location);

        var configuration = new Configuration(config);
        Assert.That(configuration.ProgressUpdateInterval, Is.EqualTo(5000));
        Assert.That(configuration.HashFirstPassSize, Is.EqualTo(65536));
        Assert.That(configuration.DegreesOfParallelism, Is.EqualTo(2));
        Assert.That(location.Source, Is.EqualTo(ConfigSource.Created));
        Assert.That(File.Exists(Path.Combine(_configFolder, "appsettings.json")), Is.True);
    }

    [Test]
    public void When_the_default_cannot_be_written_the_built_in_defaults_are_used()
    {
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "a file");
        _configFolder = Path.Combine(blocker, "cde");

        var config = ConfigBuilder.Build([], Locator(), out var location);

        Assert.That(location.Source, Is.EqualTo(ConfigSource.BuiltIn));
        Assert.That(new Configuration(config).ProgressUpdateInterval, Is.EqualTo(5000));
    }

    [Test]
    public void The_command_line_still_overrides_the_file()
    {
        Directory.CreateDirectory(_configFolder);
        File.WriteAllText(Path.Combine(_configFolder, "appsettings.json"),
            """{ "AppConfig": { "Display": { "ProgressUpdateInterval": 111 } } }""");

        var config = ConfigBuilder.Build(["--AppConfig:Display:ProgressUpdateInterval=7"], Locator(), out _);

        Assert.That(new Configuration(config).ProgressUpdateInterval, Is.EqualTo(7));
    }

    [Test]
    public void The_location_says_which_settings_are_in_use()
    {
        var created = ConfigBuilder.Build([], Locator(), out var first);
        var found = ConfigBuilder.Build([], Locator(), out var second);

        Assert.That(created, Is.Not.Null);
        Assert.That(found, Is.Not.Null);
        Assert.That(first.Describe(), Does.StartWith("Created ").And.Contain("appsettings.json"));
        Assert.That(second.Describe(), Does.StartWith("Using configuration from ").And.Contain("appsettings.json"));
        Assert.That(new ConfigLocation(ConfigSource.BuiltIn, null).Describe(), Is.EqualTo("Using the built-in defaults"));
        Assert.That(new ConfigLocation(ConfigSource.BuiltIn, null, "disk full").Describe(), Does.Contain("disk full").And.Contain("built-in defaults"));
    }
}
