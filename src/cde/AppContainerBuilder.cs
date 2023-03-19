using Autofac;
using AutofacSerilogIntegration;
using cde.Config;
using cdeLib.Module;
using MediatR.Extensions.Autofac.DependencyInjection;
using MediatR.Extensions.Autofac.DependencyInjection.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace cde;

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

    private static void ConfigureLogger(IConfiguration config)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();
    }
}