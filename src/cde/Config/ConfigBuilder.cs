using System.IO;
using Microsoft.Extensions.Configuration;

namespace cde.Config
{
    public class ConfigBuilder
    {
        public IConfigurationRoot Build(string[] args)
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())   //required for single file application or it goes hunting in temp folder when it extracts.
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();
        }
    }
}