using System;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;
using ILogger = Serilog.ILogger;

namespace cdeApi;

/// <summary>
/// <c>PUT /catalogs/{id}/description</c>. Answers the updated <see cref="CatalogInfoDto"/>. Refusals carry the reason as
/// the body: 400 too long, 404 unknown catalog, 409 a rescan/edit/load is running, 422 no file or a legacy <c>.cde</c>.
/// </summary>
public static class DescriptionEndpoint
{
    public static async Task<IResult> HandleAsync(int id, SetDescriptionRequest req, ICatalogSession session,
        HttpContext ctx, ILogger logger = null)
    {
        if (req is null) return Results.BadRequest("missing body");
        try
        {
            await session.SetDescriptionAsync(id, req.Description, ctx.RequestAborted);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.Json($"No catalog {id}.", statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (RescanAlreadyRunningException ex)
        {
            return Results.Json(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (NoSourceFileException ex)
        {
            return Results.Json(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (NotSupportedException ex)
        {
            return Results.Json(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        var updated = session.GetCatalogs().FirstOrDefault(c => c.CatalogId == id);
        return updated is null
            ? Results.Json($"Catalog {id} is unavailable; reload catalogs.", statusCode: StatusCodes.Status409Conflict)
            : Results.Json(updated);
    }
}
