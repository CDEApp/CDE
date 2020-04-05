using cdeLib.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using IConfiguration = cdeLib.Infrastructure.Config.IConfiguration;

namespace cdeLib.Infrastructure
{
    /// <summary>
    /// Configuration to talk to app.config
    /// </summary>
    public class Configuration : IConfiguration
    {
        private readonly AppConfigurationSection _appConfig;

        public Configuration(IConfigurationRoot configurationRoot)
        {
            _appConfig = configurationRoot.GetSection("AppConfig").Get<AppConfigurationSection>();
        }

        /// <summary>
        /// Get the loop interval for progress updates.
        /// </summary>
        public int ProgressUpdateInterval => _appConfig.Display.ProgressUpdateInterval;

        /// <summary>
        /// Size in bytes to use for a firstRunHashSize
        /// </summary>
        public int HashFirstPassSize => _appConfig.Hashing.FirstPassSizeInBytes;

        public int DegreesOfParallelism => _appConfig.Hashing.DegreesOfParallelism;
    }
}