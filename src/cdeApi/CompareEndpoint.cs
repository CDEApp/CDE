using System;
using System.Collections.Generic;
using System.Threading;
using cdeAppCore.Compare;
using cdeAppCore.Dtos;
using Microsoft.AspNetCore.Http;

namespace cdeApi;

/// <summary>
/// <c>POST /compare/summaries</c>: exact subtree comparison for up to <see cref="CompareService.MaxPairs"/>
/// folder pairs. 400 for a malformed ref, a non-directory, an entry past its catalog or too many pairs; 404 for
/// an unknown catalog. A client abort (<c>RequestAborted</c>) stops the walk.
/// </summary>
public static class CompareEndpoint
{
    public const int ClientClosedRequest = 499;

    public static IResult Handle(CompareSummariesRequest request, ICompareService compare, CancellationToken ct)
    {
        if (request?.Pairs is null) return Results.BadRequest("pairs required");
        if (request.Pairs.Count > CompareService.MaxPairs)
            return Results.BadRequest($"at most {CompareService.MaxPairs} pairs per request");

        var pairs = new List<(EntryRefDto Left, EntryRefDto Right)>(request.Pairs.Count);
        foreach (var pair in request.Pairs)
        {
            if (pair is null
                || !EntryRefRoute.TryParse(pair.Left, out var left)
                || !EntryRefRoute.TryParse(pair.Right, out var right))
                return Results.BadRequest("bad ref");
            pairs.Add((left, right));
        }

        try
        {
            var summaries = compare.Summarize(pairs, ct);
            return Results.Json(new CompareSummariesResponse { Summaries = [.. summaries] });
        }
        catch (CompareRequestException e)
        {
            return e.Error == CompareRequestError.NotFound ? Results.NotFound(e.Message) : Results.BadRequest(e.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Results.StatusCode(ClientClosedRequest);
        }
    }
}
