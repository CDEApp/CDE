using System;
using System.Diagnostics;
using System.IO;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// <c>cde</c> run as the real program for the settings file (#158): it starts with no appsettings.json anywhere,
/// writes a default into the config folder on first use, and reads the config folder, then the current directory,
/// then the executable's folder. The program is run from a copy of the test output folder that has no
/// appsettings.json (the test project's own would otherwise be found in the executable's folder), and every run
/// gets its own config folder through CDE_CONFIG_DIR, so nothing touches a real one.
/// </summary>
[TestFixture]
public class CdeConfigProcessTests
{
    private static string _exeCopy;

    private string _dir;
    private string _cwd;
    private string _configDir;

    [OneTimeSetUp]
    public void CopyTheProgramWithoutItsSettingsFile()
    {
        _exeCopy = Directory.CreateTempSubdirectory("cde-config-exe").FullName;
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory))
        {
            var name = Path.GetFileName(file);
            if (name.Equals(AppSettingsLocator.FileName, StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(_exeCopy, name));
        }
    }

    [OneTimeTearDown]
    public void RemoveTheCopy()
    {
        try { Directory.Delete(_exeCopy, true); } catch (IOException) { /* ignore */ }
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-config-process").FullName;
        _cwd = Path.Combine(_dir, "work");
        _configDir = Path.Combine(_dir, "config");
        Directory.CreateDirectory(_cwd);
        // The executable folder has no appsettings.json unless a test puts one there.
        var inExe = Path.Combine(_exeCopy, AppSettingsLocator.FileName);
        if (File.Exists(inExe)) File.Delete(inExe);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    private string ConfigFile => Path.Combine(_configDir, AppSettingsLocator.FileName);

    private const string Verbose =
        """{ "Serilog": { "Using": [ "Serilog.Sinks.Console" ], "MinimumLevel": "Information", "WriteTo": [ { "Name": "Console" } ] } }""";

    private const string Quiet =
        """{ "Serilog": { "Using": [ "Serilog.Sinks.Console" ], "MinimumLevel": "Warning", "WriteTo": [ { "Name": "Console" } ] } }""";

    private (int Exit, string Out, string Err) Cde(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment[AppSettingsLocator.ConfigDirVariable] = _configDir;
        start.ArgumentList.Add(Path.Combine(_exeCopy, "cde.dll"));
        foreach (var a in args) start.ArgumentList.Add(a);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.That(process.WaitForExit(60_000), Is.True, "cde did not finish");
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    [Test]
    public void Cde_starts_with_no_appsettings_json_anywhere_and_writes_the_default_to_the_config_folder()
    {
        var result = Cde("everything", "x");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err + result.Out);
        Assert.That(File.Exists(ConfigFile), Is.True, "the first run writes the default");
        Assert.That(File.ReadAllText(ConfigFile), Is.EqualTo(AppSettingsLocator.DefaultJson));
        Assert.That(Directory.GetFiles(_configDir), Has.Length.EqualTo(1), "no temporary file is left behind");
    }

    [Test]
    public void A_second_run_uses_the_file_the_first_run_wrote_and_leaves_it_alone()
    {
        Cde("everything", "x");
        var written = File.GetLastWriteTimeUtc(ConfigFile);

        var second = Cde("everything", "x");

        Assert.That(second.Exit, Is.EqualTo(0), second.Err);
        Assert.That(File.GetLastWriteTimeUtc(ConfigFile), Is.EqualTo(written));
    }

    [Test]
    public void The_which_settings_line_names_the_file_the_first_run_created_when_logging_is_turned_up()
    {
        // The default is quiet (Warning). Turning logging up through the command line shows the line.
        var result = Cde("everything", "x", "--Serilog:MinimumLevel=Information");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Contain("Created " + ConfigFile));
    }

    [Test]
    public void The_config_folder_is_read_before_the_current_directory()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(ConfigFile, Verbose);
        File.WriteAllText(Path.Combine(_cwd, "appsettings.json"), Quiet);

        var result = Cde("everything", "x");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Contain("Using configuration from " + ConfigFile));
    }

    [Test]
    public void The_which_settings_line_stays_quiet_at_the_default_level()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(ConfigFile, AppSettingsLocator.DefaultJson);

        var result = Cde("everything", "x");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Not.Contain("Using configuration"));
    }

    [Test]
    public void The_current_directory_is_used_when_the_config_folder_has_no_file()
    {
        var inCwd = Path.Combine(_cwd, "appsettings.json");
        File.WriteAllText(inCwd, Verbose);

        var result = Cde("everything", "x");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Contain("Using configuration from " + inCwd));
        Assert.That(File.Exists(ConfigFile), Is.False, "nothing is written when a file was found");
    }

    [Test]
    public void The_executable_folder_is_the_last_place_and_nothing_is_written_when_it_has_the_file()
    {
        var inExe = Path.Combine(_exeCopy, "appsettings.json");
        File.WriteAllText(inExe, Verbose);

        var result = Cde("everything", "x");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err);
        Assert.That(result.Out, Does.Contain("Using configuration from " + inExe));
        Assert.That(File.Exists(ConfigFile), Is.False);
    }

    [Test]
    public void A_config_folder_that_cannot_be_written_does_not_stop_cde()
    {
        // Nothing is found anywhere, and the config folder's parent is a file, so the default cannot be written.
        File.WriteAllText(Path.Combine(_dir, "blocker"), "a file");
        _configDir = Path.Combine(_dir, "blocker", "cde");

        var result = Cde("everything", "x", "--Serilog:MinimumLevel=Information");

        Assert.That(result.Exit, Is.EqualTo(0), result.Err + result.Out);
        Assert.That(result.Out, Does.Contain("Could not write the default settings").And.Contain("built-in defaults"));
    }
}
