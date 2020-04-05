using Autofac;
using AutofacSerilogIntegration;
using cde.Config;
using cdeLib.Module;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace cde
{
    /// <summary>
    /// Container Builder
    /// </summary>
    public static class AppContainerBuilder
    {
        public static IContainer BuildContainer(string[] args)
        {
            var builder = new ContainerBuilder();

            var config = new ConfigBuilder().Build(args);

            ConfigureLogger(config);
            builder.RegisterInstance(config);
            builder.RegisterLogger();

            builder.RegisterModule<CdelibModule>();
            Log.Logger.Debug("Building Container");
            return builder.Build();
        }

        private static void ConfigureLogger(IConfigurationRoot config)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .CreateLogger();
        }
    }
}