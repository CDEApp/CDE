using System;
using System.Configuration;

namespace cdeLib.Infrastructure
{
    public interface IConfiguration
    {
        int ProgressUpdateInterval { get; }
    }

    /// <summary>
    /// Configuration to talk to app.config
    /// </summary>
    public class Configuration : IConfiguration
    {
        /// <summary>
        /// Get the loop interval for progress updates.
        /// </summary>
        public int ProgressUpdateInterval
        {
            get
            {
                int result;
                Int32.TryParse(ConfigurationManager.AppSettings["ProgressUpdateInterval"], out result);
                if (result <= 0)
                    result = 10000;
                return result;
            }
        }
    }
}