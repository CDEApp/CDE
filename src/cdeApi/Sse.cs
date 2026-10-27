using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace cdeApi;

/// <summary>
/// Server-Sent Events response setup for the streamed endpoints (<c>/search</c>, <c>/session/reload</c>);
/// events themselves are written by <see cref="SseWriter"/>.
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
}
