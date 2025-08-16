using System;
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
    public static IContainer BuildContainer(string[] args)
    {
        var builder = new ContainerBuilder();

        try
        {
            ConfigureBootstrapLogger();
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
        catch (FileNotFoundException e)
        {
            // We'll assume it's the missing appsettings.json file error.
            Log.Logger.Error(e,
                "Please ensure there is an appsettings.json file in the same directory as the executable");
            throw;
        }
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