using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutofacSerilogIntegration;
using cde.Config;
using cdeLib.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

namespace cde;

/// <summary>
/// Container Builder
/// </summary>
public static class AppContainerBuilder
{
    /// <summary>
    /// Builds the DI container. Returns false (and leaves <paramref name="container"/> null)
    /// if appsettings.json is missing; otherwise returns true with the built container.
    /// </summary>
    public static bool TryBuildContainer(string[] args, out IContainer container)
    {
        container = null;
        ConfigureBootstrapLogger();

        // Check for appsettings.json before attempting to build
        if (!ConfigBuilder.AppSettingsExists())
        {
            WarnMissingConfig();
            return false;
        }

        var config = ConfigBuilder.Build(args);
        ConfigureLogger(config);

        var services = new ServiceCollection();
        ConfigureMessageBus(services);

        var builder = new ContainerBuilder();
        RegisterCoreServices(builder, config);
        builder.Populate(services); // surfaces SlimMessageBus + auto-declared handlers into Autofac

        container = builder.Build();
        return true;
    }

    private static void ConfigureMessageBus(IServiceCollection services)
    {
        // Add logging (required by SlimMessageBus)
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: false));

        // The cde CLI replaces some cdeLib request handlers with Spectre-progress variants. A request
        // type can only have one handler, so skip any cdeLib handler whose request type the cde assembly
        // also handles — the cde override then binds alone. New overrides are detected automatically;
        // there is no hand-maintained exclusion list. (IConsumer pub/sub events are additive, never skipped.)
        var cdeRequestTypes = RequestTypesHandledIn(typeof(AppContainerBuilder).Assembly);

        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(typeof(CdelibModule).Assembly,
                   consumerTypeFilter: t => !HandlesAnyRequest(t, cdeRequestTypes))
               .AutoDeclareFrom(typeof(AppContainerBuilder).Assembly);
        });
    }

    /// <summary>Request types handled by <see cref="IRequestHandler{T}"/> implementations in the assembly.</summary>
    private static HashSet<Type> RequestTypesHandledIn(Assembly assembly)
        => assembly.GetTypes().SelectMany(RequestTypesOf).ToHashSet();

    private static bool HandlesAnyRequest(Type handler, HashSet<Type> requestTypes)
        => RequestTypesOf(handler).Any(requestTypes.Contains);

    private static IEnumerable<Type> RequestTypesOf(Type handler)
        => handler.GetInterfaces()
            .Where(i => i.IsGenericType
                        && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                            || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .Select(i => i.GetGenericArguments()[0]);

    private static void RegisterCoreServices(ContainerBuilder builder, IConfigurationRoot config)
    {
        // Register as IConfigurationRoot (its compile-time type) — cdeLib.Infrastructure.Configuration
        // depends on IConfigurationRoot, so widening this to IConfiguration would break resolution.
        builder.RegisterInstance(config);
        builder.RegisterLogger();
        builder.RegisterModule<CdelibModule>();
        builder.RegisterType<CdeApp>();
        // Handlers are registered by SlimMessageBus AutoDeclareFrom (addServicesFromAssembly: true)
        // and surfaced into Autofac via builder.Populate(services) — no explicit handler registration needed.
    }

    private static void WarnMissingConfig()
    {
        var currentDir = Directory.GetCurrentDirectory();
        Log.Logger.Warning(
            "Configuration file '{FileName}' not found in '{Directory}'",
            ConfigBuilder.AppSettingsFileName, currentDir);
        Log.Logger.Warning(
            "Please ensure appsettings.json is in the same directory as the executable");
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