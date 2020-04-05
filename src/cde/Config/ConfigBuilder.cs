using Microsoft.Extensions.Configuration;

namespace cde.Config
{
    public class ConfigBuilder
    {
        public IConfigurationRoot Build(string[] args)
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();
        }
    }
}