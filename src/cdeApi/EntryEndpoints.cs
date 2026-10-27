using cdeAppCore.Session;
using cdeAppCore.Treemap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace cdeApi;

/// <summary>
/// The <c>/entries/{ref}/...</c> handlers. A malformed <c>ref</c> (see <see cref="EntryRefRoute"/>) is a 400.
/// Parameter names are the query-string names (minimal-API binding), so keep them in step with the client.
/// </summary>
public static class EntryEndpoints
{
    /// <summary>Directory listing / tree expand, root → entry chain, and treemap rows.</summary>
    public static IEndpointRouteBuilder MapEntryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/entries/{ref}/children", Children);
        app.MapGet("/entries/{ref}/path", Path);
        app.MapGet("/entries/{ref}/treemap", Treemap);
        return app;
    }

    /// <summary>
    /// <c>GET /entries/{ref}/children?foldersOnly&amp;skip&amp;take&amp;sort&amp;hasChildren</c>.
    /// <c>hasChildren=false</c> skips the per-folder has-sub-folder walk (a flat listing never shows expanders).
    /// </summary>
    public static IResult Children(string @ref, bool? foldersOnly, int? skip, int? take, string sort,
        bool? hasChildren, ICatalogSession session)
    {
        if (!EntryRefRoute.TryParse(@ref, out var entryRef)) return Results.BadRequest("bad ref");
        var nodes = session.GetChildren(entryRef, foldersOnly ?? false, skip ?? 0, take ?? int.MaxValue,
            hasChildren ?? true);
        return Results.Json(DirectoryNodeSort.Apply(nodes, sort));
    }

    /// <summary><c>GET /entries/{ref}/path</c>: the root → entry chain.</summary>
    public static IResult Path(string @ref, ICatalogSession session)
    {
        if (!EntryRefRoute.TryParse(@ref, out var entryRef)) return Results.BadRequest("bad ref");
        return Results.Json(session.GetPath(entryRef));
    }

    /// <summary>
    /// <c>GET /entries/{ref}/treemap?depth=3&amp;minFraction=0.0001</c>: depth-limited flat treemap rows; 404 for a
    /// catalog or entry that doesn't exist. (<see cref="EntryRefRoute"/> already rejects negative ids.)
    /// </summary>
    public static IResult Treemap(string @ref, int? depth, double? minFraction, ICatalogSession session,
        ITreemapService treemap)
    {
        if (!EntryRefRoute.TryParse(@ref, out var entryRef)) return Results.BadRequest("bad ref");
        if (entryRef.CatalogId >= session.CatalogCount) return Results.NotFound("no such entry");
        int entryCount;
        using (var lease = session.AcquireRead(entryRef.CatalogId)) entryCount = lease.Source.Count;
        if (entryRef.EntryIndex >= entryCount) return Results.NotFound("no such entry");
        return Results.Json(treemap.Build(entryRef, depth ?? 3, minFraction ?? 0));
    }
}
