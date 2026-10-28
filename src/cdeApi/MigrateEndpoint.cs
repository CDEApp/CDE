using System;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /catalogs/migrate</c>: convert the writable legacy catalogs the last load listed
/// (<see cref="ICatalogSession.LegacyCatalogs"/>) to <c>.cdex</c>. Refusals are plain JSON <see cref="MigrateError"/>s
/// before the SSE stream starts: 409 <c>busy</c> while another catalog job runs, 422 <c>nothing-to-migrate</c> when no
/// listed catalog is in a writable folder. Once started, the stream carries one <c>progress</c> event per catalog, then
/// exactly one of <c>done</c> (<see cref="MigrateResult"/>) or <c>error</c>. Aborting the request stops after the
/// catalog in progress is discarded; those already converted stay converted. Nothing is loaded: reload afterwards.
/// </summary>
public static class MigrateEndpoint
{
    public const string Busy = "busy";
    public const string NothingToMigrate = "nothing-to-migrate";
    public const string Failed = "migrate-failed";

    public static async Task HandleAsync(HttpContext ctx, ICatalogSession session)
    {
        var refusal = Refuse(session);
        if (refusal is var (status, error))
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(error, AppCoreJsonContext.Default.MigrateError,
                cancellationToken: ctx.RequestAborted);
            return;
        }

        MigrateResult result = null;
        Sse.Start(ctx.Response);
        await SseStream.RunAsync(ctx.Response.BodyWriter,
            async (progress, queue, ct) =>
            {
                try
                {
                    result = await session.MigrateLegacyCatalogsAsync(progress, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // client went away or cancelled: no event
                }
                catch (CatalogJobBusyException ex)
                {
                    await queue.WriteAsync(new MigrateError { Code = Busy, Message = ex.Message }, ct);
                }
                catch (Exception ex)
                {
                    await queue.WriteAsync(new MigrateError { Code = Failed, Message = ex.Message }, ct);
                }
            },
            (sse, _) =>
            {
                if (result != null) sse.Write("done"u8, result, AppCoreJsonContext.Default.MigrateResult);
            },
            ctx.RequestAborted);
    }

    /// <summary>The refusal for a migration that can't start, or null when it may.</summary>
    public static (int Status, MigrateError Error)? Refuse(ICatalogSession session)
    {
        if (session.AnyCatalogJobRunning)
            return (StatusCodes.Status409Conflict,
                new MigrateError { Code = Busy, Message = "Another catalog job is running; try again when it finishes." });
        if (!session.LegacyCatalogs.Any(l => l.Writable))
            return (StatusCodes.Status422UnprocessableEntity,
                new MigrateError { Code = NothingToMigrate, Message = "There are no legacy catalogs in a writable folder to migrate." });
        return null;
    }
}
