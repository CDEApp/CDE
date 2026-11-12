using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /catalogs/hash/preflight</c> (body <see cref="HashPreflightRequest"/>, 200 <see cref="HashPreflightResult"/>):
/// what a hash job with the same catalogs and options would read, before it is queued. A refusal is a JSON
/// <see cref="JobError"/> with the status <c>POST /jobs</c> gives its code: 400 <c>invalid-request</c> or
/// <c>invalid-filter</c> (the message is the one the search bar shows), 404 <c>unknown-catalog</c>, 409 <c>busy</c> or
/// <c>catalog-unavailable</c>, 422 <c>legacy-catalog</c>. The hash itself is a job (<c>POST /jobs</c>).
/// </summary>
public static class HashEndpoint
{
    public static IResult Preflight(HashPreflightRequest request, ICatalogSession session, HttpContext ctx)
    {
        try
        {
            var preview = session.PreviewHash(request?.CatalogIds, request?.Hash, ctx.RequestAborted);
            return Results.Json(preview, AppCoreJsonContext.Default.HashPreflightResult);
        }
        catch (HashRefusedException ex)
        {
            return Results.Json(ex.Error, AppCoreJsonContext.Default.JobError,
                statusCode: JobsEndpoints.StatusFor(ex.Error.Code));
        }
    }
}
