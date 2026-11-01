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
using AppSettingsLocator = cdeLib.Infrastructure.Config.AppSettingsLocator;

namespace cde;

/// <summary>
/// Container Builder
/// </summary>
public static class AppContainerBuilder
{
    /// <summary>
    /// Builds the DI container and returns true. The settings come from the file <see cref="AppSettingsLocator"/> finds,
    /// or from the built-in defaults, so a missing appsettings.json is no longer a reason to stop (#158).
    /// </summary>
    public static bool TryBuildContainer(string[] args, out IContainer container)
    {
        ConfigureBootstrapLogger();

        var config = ConfigBuilder.Build(args, new AppSettingsLocator(), out var location);
        ConfigureLogger(config);
        // Said once the configured logger is up, so it shows only when logging is turned up (the default is Warning).
        Log.Logger.Information("{ConfigurationInUse}", location.Describe());

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