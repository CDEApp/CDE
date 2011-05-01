using System;
using System.Configuration;

namespace cdeLib.Infrastructure
{
    public interface IConfiguration
    {
        int ProgressUpdateInterval { get; }
        int HashFirstPassSize { get; }
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

        /// <summary>
        /// Size in bytes to use for a firstRunHashSize
        /// </summary>
        public int HashFirstPassSize
        { get {
            int result;
            Int32.TryParse(ConfigurationManager.AppSettings["Hash.FirstPassSizeInBytes"], out result);
            if (result <= 1024)
                result = 1024;
            return result;
            
        }}
    }
}