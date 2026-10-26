using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace cdeApi;

/// <summary>
/// Minimal Server-Sent Events writer. The streamed endpoints (<c>/search</c>, <c>/session/reload</c>)
/// funnel all events through a single drain loop so writes to the response body stay single-threaded.
/// </summary>
public static class Sse
{
    /// <summary>Set SSE response headers and disable response buffering so events flush immediately.</summary>
    public static void Start(HttpResponse response)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    /// <summary>Write one SSE event. <paramref name="data"/> must be a single line (JSON is).</summary>
    public static async Task EventAsync(HttpResponse response, string eventName, string data, CancellationToken ct)
    {
        await response.WriteAsync($"event: {eventName}\n", ct);
        await response.WriteAsync($"data: {data}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
