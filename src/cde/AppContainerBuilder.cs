using System.IO;
using Autofac;
using AutofacSerilogIntegration;
using cde.Config;
using cdeLib.Module;
using MediatR.Extensions.Autofac.DependencyInjection;
using MediatR.Extensions.Autofac.DependencyInjection.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

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

        var builder = new ContainerBuilder();
        var config = new ConfigBuilder().Build(args);
        ConfigureLogger(config);
        builder.RegisterInstance(config);
        builder.RegisterType<cdeLib.Infrastructure.Logger>().As<cdeLib.Infrastructure.ILogger>();
        builder.RegisterLogger();

        builder.RegisterModule<CdelibModule>();
        var configuration = MediatRConfigurationBuilder
            .Create(typeof(AppContainerBuilder).Assembly)
            .WithAllOpenGenericHandlerTypesRegistered()
            .Build();
        builder.RegisterMediatR(configuration);
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