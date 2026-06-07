using System.IO;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutofacSerilogIntegration;
using cde.Config;
using cdeLib.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

namespace cde;

/// <summary>
/// Container Builder
/// </summary>
public static class AppContainerBuilder
{
    /// <summary>
    /// Build the DI container. Returns null if appsettings.json is missing.
    /// </summary>
    public static IContainer BuildContainer(string[] args)
    {
        ConfigureBootstrapLogger();

        // Check for appsettings.json before attempting to build
        if (!ConfigBuilder.AppSettingsExists())
        {
            var currentDir = Directory.GetCurrentDirectory();
            Log.Logger.Warning(
                "Configuration file '{FileName}' not found in '{Directory}'",
                ConfigBuilder.AppSettingsFileName, currentDir);
            Log.Logger.Warning(
                "Please ensure appsettings.json is in the same directory as the executable");
            return null;
        }

        var services = new ServiceCollection();

        // Add logging (required by SlimMessageBus)
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: false));

        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderMemory()
               // Filter out cdeLib CreateCacheCommandHandler since cde assembly overrides it
               .AutoDeclareFrom(typeof(CdelibModule).Assembly,
                   consumerTypeFilter: t => t != typeof(cdeLib.Catalog.CreateCacheCommandHandler)
                                            && t != typeof(cdeLib.Hashing.HashCatalogCommandHandler))
               .AutoDeclareFrom(typeof(AppContainerBuilder).Assembly);
        });

        var builder = new ContainerBuilder();
        var config = ConfigBuilder.Build(args);
        ConfigureLogger(config);
        builder.RegisterInstance(config);
        builder.RegisterType<cdeLib.Infrastructure.Logger>().As<cdeLib.Infrastructure.ILogger>();
        builder.RegisterLogger();

        builder.RegisterModule<CdelibModule>();

        // Populate Autofac from ServiceCollection (for SlimMessageBus)
        builder.Populate(services);

        // Register handlers explicitly in Autofac to ensure they can be resolved
        builder.RegisterType<ScanProgress.CreateCacheCommandHandler>().AsSelf();
        builder.RegisterType<ScanProgress.ScanProgressNotificationHandler>().AsSelf();
        builder.RegisterType<ScanProgress.ScanCompletedEventHandler>().AsSelf();
        builder.RegisterType<HashProgress.HashCatalogCommandHandler>().AsSelf();
        builder.RegisterType<HashProgress.HashProgressNotificationHandler>().AsSelf();
        builder.RegisterType<HashProgress.HashStatusMessageHandler>().AsSelf();
        builder.RegisterType<HashProgress.HashCompletedEventHandler>().AsSelf();
        builder.RegisterType<cdeLib.Duplicates.FindDuplicateCommandHandler>().AsSelf();
        builder.RegisterType<cdeLib.Upgrade.UpdateCommandHandler>().AsSelf();

        return builder.Build();
    }

    private static void ConfigureBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console().CreateLogger();
    }

    private static void ConfigureLogger(IConfiguration config)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();
    }
}