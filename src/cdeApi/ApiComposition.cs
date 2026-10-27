using System;
using System.Collections.Generic;
using cdeAppCore;
using cdeAppCore.Search;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using cdeAppCore.Shell;
using cdeAppCore.Treemap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ILogger = Serilog.ILogger;

namespace cdeApi;

/// <summary>The sidecar's services and routes, kept out of Program.cs so tests can compose the same app.</summary>
public static class ApiComposition
{
    public static IServiceCollection AddCdeApi(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton(logger);
        services.AddSingleton(_ => HandshakeToken.CreateRandom());

        // Both read configuration when first resolved, so sources added after this call are seen.
        services.AddSingleton(sp =>
            sp.GetRequiredService<IConfiguration>().GetSection("Cde").Get<ApiOptions>() ?? new ApiOptions());
        services.AddSingleton<IShellActions>(sp =>
        {
            // Custom command list: config-sourced and injected; core never reads config itself.
            var customCommands = sp.GetRequiredService<IConfiguration>().GetSection("CustomCommands")
                .Get<List<CustomCommandOptions>>() ?? [];
            return OperatingSystem.IsWindows()
                ? new WindowsShellActions(customCommands, logger)
                : new NoopShellActions(customCommands, logger);
        });

        // Only the requested folder: the sidecar's current directory is wherever cde-tauri was launched.
        services.AddSingleton<ILoadCatalogService>(_ => new LoadCatalogService(logger, includeCurrentDirectory: false));
        services.AddSingleton<ICatalogSession>(sp =>
            new CatalogSession(sp.GetRequiredService<ILoadCatalogService>(), logger));
        services.AddSingleton<ISearchService>(sp =>
            new SearchService(sp.GetRequiredService<ICatalogSession>()));
        services.AddSingleton<ITreemapService>(sp =>
            new TreemapService(sp.GetRequiredService<ICatalogSession>()));
        services.AddSingleton<UiStateStore>();
        services.AddSingleton<ColourRulesStore>();

        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppCoreJsonContext.Default));

        // CORS: the webview runs on a different origin (http://localhost:1420 in dev, tauri://localhost in
        // production) than this loopback sidecar. The handshake token — not the origin — is the security
        // boundary, so any origin is allowed; the token gate still checks every real request.
        services.AddCors(o => o.AddDefaultPolicy(p =>
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())); // NOSONAR S5122: loopback-only sidecar; the handshake token, not the origin, is the security boundary

        return services;
    }

    public static IEndpointRouteBuilder MapCdeApi(this IEndpointRouteBuilder app)
    {
        // /health, /catalogs, /session/reload
        app.MapSessionEndpoints();

        // /entries/{ref}/children | path | treemap
        app.MapEntryEndpoints();

        // /shell, /shell/commands
        app.MapShellEndpoints();

        // /ui-state, /colour-rules, /colour-rules/defaults
        app.MapSettingsEndpoints();

        // POST /catalogs/{id}/rescan — rescan one catalog in the background (SSE: progress..., done | error).
        // Refusals (unknown / already running / root unreachable) are plain JSON errors before the stream starts.
        app.MapPost("/catalogs/{id:int}/rescan", RescanEndpoint.HandleAsync);

        // POST /catalogs/preflight — default name, working folder, collisions for the New Catalog dialog (JSON).
        app.MapPost("/catalogs/preflight", CreateCatalogEndpoint.PreflightAsync);

        // POST /catalogs — scan a new root into a new .cdex in the working folder and add it to the session
        // (SSE: progress..., done | error). Refusals are plain JSON errors before the stream starts.
        app.MapPost("/catalogs", CreateCatalogEndpoint.HandleAsync);

        app.MapPost("/search", SearchEndpoint.HandleAsync);

        return app;
    }
}
