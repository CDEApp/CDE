using cdeLib.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using IConfiguration = cdeLib.Infrastructure.Config.IConfiguration;

namespace cdeLib.Infrastructure;

/// <summary>
/// Configuration to talk to app.config
/// </summary>
public class Configuration : IConfiguration
{
    public Configuration(IConfigurationRoot configurationRoot)
    {
        Config = configurationRoot.GetSection("AppConfig").Get<AppConfigurationSection>();
    }

    public AppConfigurationSection Config { get; }

    /// <summary>
    /// Get the loop interval for progress updates.
    /// </summary>
    public int ProgressUpdateInterval => Config.Display.ProgressUpdateInterval;

    /// <summary>
    /// Size in bytes to use for a firstRunHashSize
    /// </summary>
    public int HashFirstPassSize => Config.Hashing.FirstPassSizeInBytes;

    public int DegreesOfParallelism => Config.Hashing.DegreesOfParallelism;
}