using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace cdeApi;

/// <summary>
/// The handshake-token gate: every request must present the <see cref="HandshakeToken"/>, either in the
/// <c>X-CDE-Token</c> header or as <c>Authorization: Bearer &lt;token&gt;</c>, or it gets a 401. CORS preflight
/// (<c>OPTIONS</c>, sent without the token) passes through, so register this after <c>UseCors</c>.
/// </summary>
public static class TokenGate
{
    public const string HeaderName = "X-CDE-Token";

    public static IApplicationBuilder UseTokenGate(this IApplicationBuilder app)
    {
        var token = app.ApplicationServices.GetRequiredService<HandshakeToken>().Value;
        return app.Use((ctx, next) => InvokeAsync(ctx, next, token));
    }

    public static async Task InvokeAsync(HttpContext ctx, Func<Task> next, string token)
    {
        // CORS preflight is answered by UseCors and carries no token; let it through.
        if (HttpMethods.IsOptions(ctx.Request.Method))
        {
            await next();
            return;
        }

        var provided = ctx.Request.Headers[HeaderName].FirstOrDefault();
        if (provided is null)
        {
            var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
            if (auth is not null && auth.StartsWith("Bearer ", StringComparison.Ordinal))
                provided = auth["Bearer ".Length..];
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(provided ?? ""),
                Encoding.ASCII.GetBytes(token)))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("unauthorized", ctx.RequestAborted);
            return;
        }

        await next();
    }
}
