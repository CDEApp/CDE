using System;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /catalogs/preflight</c> (body <see cref="CreatePreflightRequest"/>, 200 <see cref="CreatePreflightResult"/>)
/// and <c>POST /catalogs</c> (body <see cref="CreateCatalogRequest"/>). The create runs the preflight checks
/// before the SSE stream starts and refuses with a plain JSON <see cref="CreateCatalogError"/> and a distinct status:
/// 400 <c>invalid-file-name</c>, 409 <c>busy</c> / <c>file-exists</c> (carries <c>existingCatalogId</c> when that file is
/// a loaded catalog), 422 <c>no-catalog-dir</c> / <c>root-not-accessible</c> / <c>folder-not-writable</c>. Once the
/// stream has started it carries <c>progress</c> events, then exactly one of <c>done</c> or <c>error</c>. Aborting the
/// request cancels the scan; nothing is added to the session or left on disk.
/// </summary>
public static class CreateCatalogEndpoint
{
    public const string InvalidFileName = "invalid-file-name";
    public const string Busy = "busy";
    public const string FileExists = "file-exists";
    public const string NoCatalogDir = "no-catalog-dir";
    public const string RootNotAccessible = "root-not-accessible";
    public const string FolderNotWritable = "folder-not-writable";
    public const string Failed = "create-failed";

    public static async Task PreflightAsync(HttpContext ctx, CreatePreflightRequest request, ICatalogSession session)
    {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        await ctx.Response.WriteAsJsonAsync(session.PreflightCreate(request ?? new CreatePreflightRequest()),
            AppCoreJsonContext.Default.CreatePreflightResult, cancellationToken: ctx.RequestAborted);
    }

    public static async Task HandleAsync(HttpContext ctx, CreateCatalogRequest request, ICatalogSession session)
    {
        request ??= new CreateCatalogRequest();
        var refusal = Refuse(session.PreflightCreate(
            new CreatePreflightRequest { RootPath = request.RootPath, FileName = request.FileName }), request);
        if (refusal is var (status, error))
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(error, AppCoreJsonContext.Default.CreateCatalogError, cancellationToken: ctx.RequestAborted);
            return;
        }

        CreateCatalogResult result = null;
        Sse.Start(ctx.Response);
        await SseStream.RunAsync(ctx.Response.BodyWriter,
            async (progress, queue, ct) =>
            {
                try
                {
                    result = await session.CreateCatalogAsync(request, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // client went away or cancelled: no event, nothing was added
                }
                catch (Exception ex)
                {
                    await queue.WriteAsync(ToError(ex, request), ct);
                }
            },
            (sse, _) =>
            {
                if (result != null) sse.Write("done"u8, result, AppCoreJsonContext.Default.CreateCatalogResult);
            },
            ctx.RequestAborted);
    }

    /// <summary>The refusal for a preflight that can't proceed, or null when the create may start. Checked in this order.</summary>
    public static (int Status, CreateCatalogError Error)? Refuse(CreatePreflightResult pf, CreateCatalogRequest request)
    {
        if (pf.Busy)
            return (StatusCodes.Status409Conflict,
                new CreateCatalogError { Code = Busy, Message = "Catalogs are being loaded or migrated; try again when that finishes." });
        if (pf.FileNameError != null)
            return (StatusCodes.Status400BadRequest, new CreateCatalogError { Code = InvalidFileName, Message = pf.FileNameError });
        if (pf.CatalogDir == null)
            return (StatusCodes.Status422UnprocessableEntity,
                new CreateCatalogError { Code = NoCatalogDir, Message = new NoCatalogDirException().Message });
        if (!pf.RootAccessible)
            return (StatusCodes.Status422UnprocessableEntity,
                new CreateCatalogError
                {
                    Code = RootNotAccessible,
                    RootPath = request.RootPath,
                    Message = $"{request.RootPath} can't be reached (drive disconnected, path moved, or no permission)."
                });
        if (!pf.Writable)
            return (StatusCodes.Status422UnprocessableEntity,
                new CreateCatalogError { Code = FolderNotWritable, Message = new CatalogFolderNotWritableException(pf.CatalogDir).Message });
        if (pf.Exists)
            return (StatusCodes.Status409Conflict,
                new CreateCatalogError
                {
                    Code = FileExists,
                    ExistingCatalogId = pf.ExistingCatalogId,
                    Message = new CatalogFileExistsException(pf.TargetPath, pf.ExistingCatalogId).Message
                });
        return null;
    }

    // A failure after the stream started (a race the preflight could not see, or a scan/IO error).
    private static CreateCatalogError ToError(Exception ex, CreateCatalogRequest request) => ex switch
    {
        CatalogFileExistsException e => new CreateCatalogError { Code = FileExists, Message = e.Message, ExistingCatalogId = e.ExistingCatalogId },
        InvalidCatalogFileNameException e => new CreateCatalogError { Code = InvalidFileName, Message = e.Message },
        CatalogFolderNotWritableException e => new CreateCatalogError { Code = FolderNotWritable, Message = e.Message },
        NoCatalogDirException e => new CreateCatalogError { Code = NoCatalogDir, Message = e.Message },
        CatalogJobBusyException e => new CreateCatalogError { Code = Busy, Message = e.Message },
        RootNotAccessibleException e => new CreateCatalogError { Code = RootNotAccessible, Message = e.Message, RootPath = e.RootPath ?? request.RootPath },
        _ => new CreateCatalogError { Code = Failed, Message = ex.Message }
    };
}
