using System.IO;
using Microsoft.Extensions.Configuration;

namespace cde.Config;

public class ConfigBuilder
{
    public const string AppSettingsFileName = "appsettings.json";

    public static bool AppSettingsExists()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), AppSettingsFileName);
        return File.Exists(path);
    }

    public static IConfigurationRoot Build(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory
                .GetCurrentDirectory()) // required for a single file application, or it goes hunting in the temp folder when it extracts.
            .AddJsonFile(AppSettingsFileName, optional: true, reloadOnChange: true)
            .AddCommandLine(args)
            .Build();
    }
}