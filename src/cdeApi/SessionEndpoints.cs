using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore.Jobs;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace cdeApi;

/// <summary>Liveness, the catalog list and status-bar figures, and reloading the session.</summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", Health);
        app.MapGet("/catalogs", Catalogs);
        app.MapPost("/session/reload", ReloadAsync);
        return app;
    }

    public static IResult Health() => Results.Ok(new { status = "ok" });

    /// <summary><c>GET /catalogs</c>: the loaded catalogs plus the status-bar figures.</summary>
    public static IResult Catalogs(ICatalogSession session)
    {
        using var proc = Process.GetCurrentProcess();
        return Results.Json(new CatalogsResponse
        {
            Catalogs = session.GetCatalogs(),
            CatalogDir = session.CatalogDir,
            CatalogsLoaded = session.CatalogCount,
            TotalEntries = session.TotalEntryCount,
            MemoryBytes = proc.PrivateMemorySize64,
            LegacyCatalogs = session.LegacyCatalogs
        });
    }

    /// <summary>
    /// <c>POST /session/reload</c>: reload catalogs, streaming load progress over SSE then a final <c>done</c>.
    /// Optional <c>?path=&lt;dir&gt;</c> loads .cdex catalogs from that folder (and one level down); otherwise
    /// <see cref="ApiOptions.ConfigPath"/> is used. 409 while any job is queued or running, or a migration runs.
    /// </summary>
    public static async Task ReloadAsync(HttpContext ctx, ICatalogSession session, ApiOptions options, JobRegistry jobs)
    {
        var requested = ctx.Request.Query["path"].FirstOrDefault();
        var loadPath = string.IsNullOrWhiteSpace(requested) ? options.ConfigPath : requested;

        using var exclusive = jobs.TryBeginExclusive();
        if (exclusive == null || session.AnyCatalogJobRunning)
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsync("busy: catalog jobs are queued or running", ctx.RequestAborted);
            return;
        }

        Sse.Start(ctx.Response);
        await SseStream.RunAsync(ctx.Response.BodyWriter,
            (progress, _, ct) => session.LoadAsync(loadPath, progress, ct),
            (sse, _) => sse.WriteDone(session.CatalogCount, (long)session.TotalEntryCount),
            ctx.RequestAborted);
    }
}
