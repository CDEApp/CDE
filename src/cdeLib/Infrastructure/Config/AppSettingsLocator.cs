using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.FileProviders;

namespace cdeLib.Infrastructure.Config;

/// <summary>Where the settings in use came from.</summary>
public enum ConfigSource
{
    /// <summary>appsettings.json in the config folder.</summary>
    ConfigFolder,

    /// <summary>appsettings.json in the current directory.</summary>
    CurrentDirectory,

    /// <summary>appsettings.json in the executable's folder.</summary>
    ExecutableFolder,

    /// <summary>There was none, and this run wrote the default to the config folder.</summary>
    Created,

    /// <summary>There was none and the default could not be written, so the built-in defaults are used.</summary>
    BuiltIn,
}

/// <summary>The settings file to use. <see cref="Path"/> is null for <see cref="ConfigSource.BuiltIn"/>.</summary>
/// <param name="Source">Where it was found.</param>
/// <param name="Path">The file, or null for the built-in defaults.</param>
/// <param name="Error">Why the default could not be written, for <see cref="ConfigSource.BuiltIn"/>.</param>
public sealed record ConfigLocation(ConfigSource Source, string Path, string Error = null)
{
    /// <summary>One line saying which settings are in use, for the log.</summary>
    public string Describe() => Source switch
    {
        ConfigSource.Created => $"Created {Path} with the default settings",
        ConfigSource.BuiltIn when Error != null => $"Could not write the default settings ({Error}); using the built-in defaults",
        ConfigSource.BuiltIn => "Using the built-in defaults",
        _ => $"Using configuration from {Path}",
    };
}

/// <summary>
/// Finds <c>appsettings.json</c> for <c>cde</c> and <c>cdeApi</c> (#158). It looks in the config folder, then the
/// current directory, then the executable's folder, and uses the first file it finds on its own (no merging). When
/// there is none it writes <see cref="DefaultJson"/> to the config folder, and when that fails it uses the same text
/// as built-in defaults.
/// </summary>
/// <remarks>
/// The config folder is <c>$CDE_CONFIG_DIR</c> when that is set, otherwise <c>cde</c> inside the application data
/// folder: <c>$XDG_CONFIG_HOME/cde</c> or <c>~/.config/cde</c> on Linux and macOS, <c>%APPDATA%\cde</c> on Windows.
/// That is the folder that already holds ui-state.json, colour-rules.txt and shell.json.
/// </remarks>
public sealed class AppSettingsLocator
{
    public const string FileName = "appsettings.json";

    /// <summary>The environment variable that replaces the config folder.</summary>
    public const string ConfigDirVariable = "CDE_CONFIG_DIR";

    /// <summary>
    /// The settings written when there is no file, and used as the built-in defaults: one text, so the two cannot
    /// differ. No Seq sink (it needs a server on localhost:5341), unlike the file shipped beside the executables.
    /// </summary>
    public const string DefaultJson = """
        {
          "AppConfig": {
            "Display": {
              "ProgressUpdateInterval": 5000,
              "ConsoleLogToSeq": true
            },
            "Hashing": {
              "FirstPassSizeInBytes": 65536,
              "DegreesOfParallelism": 2
            }
          },
          "Serilog": {
            "Using": [
              "Serilog.Sinks.Console"
            ],
            "MinimumLevel": "Warning",
            "WriteTo": [
              {
                "Name": "Console"
              }
            ],
            "Enrich": [
              "FromLogContext"
            ]
          },
          "Cde": {
            "ConfigPath": "."
          },
          "CustomCommands": []
        }
        """;

    private static readonly Lazy<Dictionary<string, string>> BuiltInValues = new(() =>
    {
        var parsed = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(DefaultJson))).Build();
        return parsed.AsEnumerable()
            .Where(pair => pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    });

    private readonly string _currentDirectory;
    private readonly string _executableFolder;

    /// <param name="getEnvironmentVariable">Reads an environment variable; the real environment when null.</param>
    /// <param name="appDataFolder">The application data folder, which need not exist; <see cref="Environment.SpecialFolder.ApplicationData"/> when null.</param>
    /// <param name="currentDirectory">The current directory; the real one when null.</param>
    /// <param name="executableFolder">The executable's folder; <see cref="AppContext.BaseDirectory"/> when null.</param>
    public AppSettingsLocator(
        Func<string, string> getEnvironmentVariable = null,
        string appDataFolder = null,
        string currentDirectory = null,
        string executableFolder = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        // DoNotVerify: on Linux the default option returns an empty string when ~/.config does not exist yet (a new
        // account, a bare container), which would leave no config folder. The folder is created when the default is written.
        appDataFolder ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        _currentDirectory = currentDirectory ?? Directory.GetCurrentDirectory();
        _executableFolder = executableFolder ?? AppContext.BaseDirectory;

        var overridden = AbsoluteOrNull(getEnvironmentVariable(ConfigDirVariable));
        ConfigFolder = overridden
            ?? (string.IsNullOrEmpty(appDataFolder) ? null // no home folder (HOME unset): there is no user config folder
                : Path.Combine(appDataFolder, "cde"));
    }

    // A relative CDE_CONFIG_DIR (./cfg) means relative to the current directory; the file provider needs a full path.
    // A value that is not a usable path is ignored, so the default folder is used and cde still starts.
    private static string AbsoluteOrNull(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>The user config folder; null when there is none (no override, and no application data folder).</summary>
    public string ConfigFolder { get; }

    /// <summary>Finds the settings file, writing the default when there is none.</summary>
    public ConfigLocation Locate()
    {
        (string Folder, ConfigSource Source)[] places =
        [
            (ConfigFolder, ConfigSource.ConfigFolder),
            (_currentDirectory, ConfigSource.CurrentDirectory),
            (_executableFolder, ConfigSource.ExecutableFolder),
        ];

        foreach (var (folder, source) in places)
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path)) return new ConfigLocation(source, path);
        }

        return WriteDefault();
    }

    // Writes a temporary file and moves it into place without overwriting, so a reader never sees half a file, and
    // two programs starting together on a fresh machine cannot clobber each other: the loser finds the winner's file.
    private ConfigLocation WriteDefault()
    {
        if (ConfigFolder == null)
            return new ConfigLocation(ConfigSource.BuiltIn, null, "there is no user config folder");

        var target = Path.Combine(ConfigFolder, FileName);
        string temp = null;
        try
        {
            Directory.CreateDirectory(ConfigFolder);
            temp = Path.Combine(ConfigFolder, $"{FileName}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, DefaultJson, new UTF8Encoding(false));
            try
            {
                File.Move(temp, target, overwrite: false);
                temp = null;
                return new ConfigLocation(ConfigSource.Created, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                return new ConfigLocation(ConfigSource.ConfigFolder, target); // another program created it first
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or ArgumentException or SecurityException)
        {
            return new ConfigLocation(ConfigSource.BuiltIn, null, ex.Message);
        }
        finally
        {
            if (temp != null)
            {
                try { File.Delete(temp); } catch (Exception) { /* a leftover temporary file is harmless */ }
            }
        }
    }

    /// <summary>
    /// A configuration source for the located file, or for the built-in defaults when there is no file. A source of a
    /// file reloads when the file changes only if asked.
    /// </summary>
    public static IConfigurationSource CreateSource(ConfigLocation location, bool reloadOnChange = false)
    {
        ArgumentNullException.ThrowIfNull(location);
        // The built-in defaults are plain values, not a stream: a stream can be read once, and a configuration that is
        // rebuilt or reloaded would find it empty.
        if (location.Path == null)
            return new MemoryConfigurationSource { InitialData = BuiltInValues.Value };

        return new JsonConfigurationSource
        {
            FileProvider = new PhysicalFileProvider(Path.GetDirectoryName(location.Path)!),
            Path = Path.GetFileName(location.Path),
            Optional = false,
            ReloadOnChange = reloadOnChange,
        };
    }
}
