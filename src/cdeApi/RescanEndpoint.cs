using System;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /catalogs/{id}/rescan</c>. Pre-checks run before the SSE stream starts and answer with a plain JSON
/// <see cref="RescanError"/> and a distinct status: 404 <c>unknown-catalog</c>, 409 <c>already-running</c>,
/// 422 <c>root-not-accessible</c> (the UI shows its "can't be reached" modal for this one). Once the stream has
/// started it carries <c>progress</c> events, then exactly one of <c>done</c> or <c>error</c>. Aborting the request
/// cancels the scan; nothing is replaced until the very end, so a cancel leaves the catalog untouched.
/// </summary>
public static class RescanEndpoint
{
    public const string UnknownCatalog = "unknown-catalog";
    public const string AlreadyRunning = "already-running";
    public const string RootNotAccessible = "root-not-accessible";
    public const string Failed = "rescan-failed";

    public static async Task HandleAsync(HttpContext ctx, int id, ICatalogSession session)
    {
        var preflight = session.PreflightRescan(id);
        if (preflight != RescanPreflight.Ready)
        {
            await WriteRefusalAsync(ctx, id, preflight, session);
            return;
        }

        RescanResult result = null;
        Sse.Start(ctx.Response);
        await SseStream.RunAsync(ctx.Response.BodyWriter,
            async (progress, queue, ct) =>
            {
                try
                {
                    result = await session.RescanAsync(id, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // client went away or cancelled: no event, the catalog is untouched
                }
                catch (RootNotAccessibleException ex)
                {
                    await queue.WriteAsync(
                        new RescanError { Code = RootNotAccessible, Message = ex.Message, RootPath = ex.RootPath }, ct);
                }
                catch (RescanAlreadyRunningException ex)
                {
                    await queue.WriteAsync(new RescanError { Code = AlreadyRunning, Message = ex.Message }, ct);
                }
                catch (Exception ex)
                {
                    await queue.WriteAsync(new RescanError { Code = Failed, Message = ex.Message }, ct);
                }
            },
            (sse, _) =>
            {
                if (result != null) sse.Write("done"u8, result, AppCoreJsonContext.Default.RescanResult);
            },
            ctx.RequestAborted);
    }

    private static async Task WriteRefusalAsync(HttpContext ctx, int id, RescanPreflight preflight, ICatalogSession session)
    {
        var (status, error) = preflight switch
        {
            RescanPreflight.UnknownCatalog => (StatusCodes.Status404NotFound,
                new RescanError { Code = UnknownCatalog, Message = $"No catalog {id}." }),
            RescanPreflight.AlreadyRunning => (StatusCodes.Status409Conflict,
                new RescanError { Code = AlreadyRunning, Message = $"A rescan of catalog {id} is already running." }),
            _ => (StatusCodes.Status422UnprocessableEntity, NotAccessible(session, id))
        };
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(error, AppCoreJsonContext.Default.RescanError);
    }

    private static RescanError NotAccessible(ICatalogSession session, int id)
    {
        var rootPath = session.GetCatalogs()[id].RootPath;
        return new RescanError
        {
            Code = RootNotAccessible,
            RootPath = rootPath,
            Message = new RootNotAccessibleException(rootPath).Message
        };
    }
}
