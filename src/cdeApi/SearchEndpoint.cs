using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeAppCore.Validation;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /search</c>: validate first (400 + message), then stream <c>result</c>/<c>progress</c>/<c>done</c> over
/// SSE. Cancels on client disconnect via <c>RequestAborted</c>.
/// </summary>
public static class SearchEndpoint
{
    public static async Task HandleAsync(HttpContext ctx, SearchQuery query, ISearchService search)
    {
        var v = SearchFilterValidator.Validate(
            query.RegexMode, query.Pattern,
            query.FromSizeEnable, query.FromSize, query.ToSizeEnable, query.ToSize,
            query.FromDateEnable, query.FromDate, query.ToDateEnable, query.ToDate,
            query.FromHourEnable, query.FromHour, query.ToHourEnable, query.ToHour);
        if (!v.IsValid)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync(v.Message);
            return;
        }

        Sse.Start(ctx.Response);
        await SseStream.RunAsync(ctx.Response.BodyWriter,
            async (progress, queue, ct) =>
            {
                await foreach (var row in search.SearchAsync(query, progress, ct))
                {
                    // Bounded queue: waits when the client can't keep up, so the search is held back too.
                    await queue.WriteAsync(row, ct);
                }
            },
            (sse, count) => sse.WriteDone(count),
            ctx.RequestAborted);
    }
}
