using System;
using System.IO;
using cdeApi;
using cdeLib.Infrastructure.Config;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>
/// How the cdeApi sidecar finds its settings (#158): the same lookup as cde (config folder, current directory,
/// executable folder; the first file alone; a default written when there is none), in place of the file ASP.NET finds
/// by default, with the environment and the command line still on top, and shell.json still layered over it.
/// </summary>
[TestFixture]
public class ApiConfigurationTests
{
    private string _root;
    private string _configFolder;
    private string _contentRoot;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("cdeapi-config").FullName;
        _configFolder = Path.Combine(_root, "config");
        _contentRoot = Path.Combine(_root, "content");
        Directory.CreateDirectory(_contentRoot);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { /* ignore */ }
    }

    private AppSettingsLocator Locator() =>
        new(name => name == AppSettingsLocator.ConfigDirVariable ? _configFolder : null,
            appDataFolder: Path.Combine(_root, "appdata"), currentDirectory: _contentRoot,
            executableFolder: Path.Combine(_root, "exe"));

    // The default host builder loads appsettings.json from its content root, which is what the sidecar used to read.
    private WebApplicationBuilder Builder(params string[] args) =>
        WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _contentRoot, Args = args });

    private static void Put(string folder, string name, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), text);
    }

    [Test]
    public void The_config_folder_file_replaces_the_one_the_host_finds_by_default()
    {
        Put(_contentRoot, "appsettings.json", """{ "Cde": { "ConfigPath": "from-the-content-root" } }""");
        Put(_configFolder, "appsettings.json", """{ "Cde": { "ConfigPath": "from-the-config-folder" } }""");
        var builder = Builder();
        Assert.That(builder.Configuration["Cde:ConfigPath"], Is.EqualTo("from-the-content-root"), "test setup: the host reads its own");

        var location = ApiConfiguration.UseAppSettings(builder.Configuration, Locator());

        Assert.That(builder.Configuration["Cde:ConfigPath"], Is.EqualTo("from-the-config-folder"));
        Assert.That(location.Source, Is.EqualTo(ConfigSource.ConfigFolder));
    }

    [Test]
    public void The_first_file_is_used_on_its_own()
    {
        Put(_contentRoot, "appsettings.json", """{ "Cde": { "ConfigPath": "x" }, "OnlyHere": "content root" }""");
        Put(_configFolder, "appsettings.json", """{ "Cde": { "ConfigPath": "from-the-config-folder" } }""");
        var builder = Builder();

        ApiConfiguration.UseAppSettings(builder.Configuration, Locator());

        Assert.That(builder.Configuration["OnlyHere"], Is.Null, "the content root's file is not merged in");
    }

    [Test]
    public void The_command_line_and_the_environment_still_win_over_the_file()
    {
        Put(_configFolder, "appsettings.json", """{ "Cde": { "ConfigPath": "from-the-config-folder" } }""");
        var builder = Builder("--Cde:ConfigPath=from-the-command-line");

        ApiConfiguration.UseAppSettings(builder.Configuration, Locator());

        Assert.That(builder.Configuration["Cde:ConfigPath"], Is.EqualTo("from-the-command-line"));
    }

    [Test]
    public void With_no_file_anywhere_the_default_is_written_and_used()
    {
        var builder = Builder();

        var location = ApiConfiguration.UseAppSettings(builder.Configuration, Locator());

        Assert.That(location.Source, Is.EqualTo(ConfigSource.Created));
        Assert.That(File.ReadAllText(Path.Combine(_configFolder, "appsettings.json")), Is.EqualTo(AppSettingsLocator.DefaultJson));
        Assert.That(builder.Configuration["Cde:ConfigPath"], Is.EqualTo("."));
    }

    [Test]
    public void When_the_default_cannot_be_written_the_built_in_defaults_are_used()
    {
        File.WriteAllText(Path.Combine(_root, "blocker"), "a file");
        _configFolder = Path.Combine(_root, "blocker", "cde");
        var builder = Builder();

        var location = ApiConfiguration.UseAppSettings(builder.Configuration, Locator());

        Assert.That(location.Source, Is.EqualTo(ConfigSource.BuiltIn));
        Assert.That(builder.Configuration["Cde:ConfigPath"], Is.EqualTo("."));
    }

    [Test]
    public void Shell_json_lives_in_the_config_folder_and_layers_over_appsettings()
    {
        Put(_configFolder, "appsettings.json", """{ "CustomCommands": [ { "Label": "from appsettings" } ] }""");
        Put(_configFolder, "shell.json", """{ "CustomCommands": [ { "Label": "from shell.json" } ] }""");
        var builder = Builder();

        var locator = Locator();
        ApiConfiguration.UseAppSettings(builder.Configuration, locator);
        ApiConfiguration.AddShellJson(builder.Configuration, locator);

        Assert.That(ApiConfiguration.ShellJsonPath(locator), Is.EqualTo(Path.Combine(_configFolder, "shell.json")));
        Assert.That(builder.Configuration["CustomCommands:0:Label"], Is.EqualTo("from shell.json"));
    }

    [Test]
    public void A_missing_shell_json_is_fine()
    {
        var builder = Builder();
        var locator = Locator();
        ApiConfiguration.UseAppSettings(builder.Configuration, locator);

        Assert.DoesNotThrow(() => ApiConfiguration.AddShellJson(builder.Configuration, locator));
    }

    // ---- the other files in the config folder follow CDE_CONFIG_DIR too ----

    [Test]
    public void The_ui_state_and_colour_rules_files_default_to_the_config_folder()
    {
        // The fixture points CDE_CONFIG_DIR at a temporary folder for this whole run.
        var folder = Environment.GetEnvironmentVariable(AppSettingsLocator.ConfigDirVariable)!;

        _ = new UiStateStore();
        new ColourRulesStore().Read(); // creates its file from the defaults when missing

        Assert.That(Directory.Exists(folder), Is.True);
        Assert.That(File.Exists(Path.Combine(folder, "colour-rules.txt")), Is.True);
    }
}
