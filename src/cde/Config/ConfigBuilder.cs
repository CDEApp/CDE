using cdeLib.Infrastructure.Config;
using Microsoft.Extensions.Configuration;

namespace cde.Config;

public static class ConfigBuilder
{
    public const string AppSettingsFileName = AppSettingsLocator.FileName;

    public static IConfigurationRoot Build(string[] args) => Build(args, new AppSettingsLocator(), out _);

    /// <summary>
    /// The settings from the file the locator finds (the config folder, then the current directory, then the
    /// executable's folder; the default is written when there is none), with the command line on top. When the default
    /// cannot be written the built-in defaults are used, so a missing file is never an error (#158).
    /// </summary>
    public static IConfigurationRoot Build(string[] args, AppSettingsLocator locator, out ConfigLocation location)
    {
        location = locator.Locate();
        return new ConfigurationBuilder()
            .Add(AppSettingsLocator.CreateSource(location, reloadOnChange: true)) // an absolute file, so no base path is needed
            .AddCommandLine(args)
            .Build();
    }
}