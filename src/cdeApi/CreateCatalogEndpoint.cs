using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /catalogs/preflight</c> (body <see cref="CreatePreflightRequest"/>, 200 <see cref="CreatePreflightResult"/>):
/// what the New Catalog dialog shows before Create. The create itself is a job (<c>POST /jobs</c>).
/// </summary>
public static class CreateCatalogEndpoint
{
    public static async Task PreflightAsync(HttpContext ctx, CreatePreflightRequest request, ICatalogSession session)
    {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        await ctx.Response.WriteAsJsonAsync(session.PreflightCreate(request ?? new CreatePreflightRequest()),
            AppCoreJsonContext.Default.CreatePreflightResult, cancellationToken: ctx.RequestAborted);
    }
}
