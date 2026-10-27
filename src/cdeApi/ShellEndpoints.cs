using System.Linq;
using cdeAppCore.Session;
using cdeAppCore.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace cdeApi;

/// <summary>The shell actions (open / explore / properties / custom command) and the custom-command menu.</summary>
public static class ShellEndpoints
{
    public static IEndpointRouteBuilder MapShellEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/shell", Run);
        app.MapGet("/shell/commands", Commands);
        return app;
    }

    /// <summary>
    /// <c>POST /shell</c>, entry-ref guarded (D11): resolve the path from the session, re-check existence, then
    /// invoke the shell action in this (sidecar) process. Never accepts a client-supplied path.
    /// </summary>
    public static IResult Run(ShellRequest req, ICatalogSession session, IShellActions shell)
    {
        if (req is null || !EntryRefRoute.TryParse(req.Ref, out var entryRef))
            return Results.BadRequest("bad ref");
        if (!session.ExistsOnFileSystem(entryRef))
            return Results.BadRequest("entry not present on this filesystem");

        var path = session.ResolveFullPath(entryRef);
        switch (req.Action)
        {
            case "open": shell.Open(path); break;
            case "explore": shell.Explore(path); break;
            case "properties": shell.ShowProperties(path); break;
            case "custom": shell.RunCustomCommand(req.CommandId ?? -1, path); break;
            default: return Results.BadRequest("unknown action");
        }

        return Results.Ok();
    }

    /// <summary><c>GET /shell/commands</c>: the configured custom commands a frontend renders as menu items (D10).</summary>
    public static IResult Commands(IShellActions shell) =>
        Results.Json(shell.CustomCommands
            .Select((c, i) => new ShellCommandInfo { Id = i, Label = c.Label })
            .ToList());
}
