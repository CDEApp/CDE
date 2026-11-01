using System;
using System.IO;
using System.Linq;
using cdeLib.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace cdeApi;

/// <summary>
/// The sidecar's settings files (#158): the same lookup as the <c>cde</c> command line, and the user-scoped
/// <c>shell.json</c> in the same config folder.
/// </summary>
public static class ApiConfiguration
{
    /// <summary>
    /// Replaces the appsettings.json that the default host builder reads from its content root with the file
    /// <see cref="AppSettingsLocator"/> finds (the config folder, then the current directory, then the executable's
    /// folder; the first file alone; a default written when there is none), or with the built-in defaults when that
    /// cannot be written. The new source takes the old one's place, so the environment variables and the command line
    /// added after it still win.
    /// </summary>
    public static ConfigLocation UseAppSettings(ConfigurationManager configuration, AppSettingsLocator locator)
    {
        var location = locator.Locate();
        var source = AppSettingsLocator.CreateSource(location, reloadOnChange: true);

        var sources = configuration.Sources;
        var index = sources.ToList().FindIndex(s =>
            s is JsonConfigurationSource json && string.Equals(json.Path, AppSettingsLocator.FileName, StringComparison.Ordinal));
        if (index >= 0) sources[index] = source;
        else sources.Insert(0, source);

        return location;
    }

    /// <summary>The user-scoped shell.json (custom commands), in the config folder; null when there is no config folder.</summary>
    public static string ShellJsonPath(AppSettingsLocator locator) =>
        locator.ConfigFolder == null ? null : Path.Combine(locator.ConfigFolder, "shell.json");

    /// <summary>Layers the optional shell.json over the settings. It is read once, at startup.</summary>
    public static void AddShellJson(ConfigurationManager configuration, AppSettingsLocator locator)
    {
        var path = ShellJsonPath(locator);
        if (path != null) configuration.AddJsonFile(path, optional: true, reloadOnChange: false);
    }

    /// <summary>The config folder for the files cdeApi keeps itself (ui-state.json, colour-rules.txt).</summary>
    public static string ConfigFolder() => new AppSettingsLocator().ConfigFolder ?? "cde";
}
