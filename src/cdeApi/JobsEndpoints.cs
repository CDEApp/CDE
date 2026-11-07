using System;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Jobs;
using cdeAppCore.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace cdeApi;

/// <summary>
/// Catalog scan jobs (rescan, create) owned by the sidecar's <see cref="JobRegistry"/>. <c>POST /jobs</c> answers 202
/// with the queued job, or refuses with a JSON <see cref="JobError"/>: 404 <c>unknown-catalog</c>; 409
/// <c>already-running</c>, <c>busy</c>, <c>catalog-unavailable</c>, <c>file-exists</c>; 422 <c>root-not-accessible</c>,
/// <c>no-source-file</c>, <c>invalid-file-name</c>, <c>no-catalog-dir</c>, <c>folder-not-writable</c>; 400
/// <c>invalid-request</c>. Progress and outcomes arrive on <c>GET /jobs/events</c>.
/// </summary>
public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/jobs", Submit);
        app.MapGet("/jobs", List);
        app.MapGet("/jobs/events", EventsAsync);
        app.MapPost("/jobs/{id:long}/cancel", Cancel);
        app.MapDelete("/jobs/finished", DismissFinished);
        app.MapDelete("/jobs/{id:long}", Dismiss);
        return app;
    }

    public static IResult Submit(JobSubmitRequest request, JobRegistry jobs)
    {
        var submission = jobs.Submit(request);
        return submission.Refusal is { } refusal
            ? Results.Json(refusal, AppCoreJsonContext.Default.JobError, statusCode: StatusFor(refusal.Code))
            : Results.Json(submission.Job, AppCoreJsonContext.Default.JobDto, statusCode: StatusCodes.Status202Accepted);
    }

    public static int StatusFor(string code) => code switch
    {
        JobErrorCodes.UnknownCatalog => StatusCodes.Status404NotFound,
        JobErrorCodes.AlreadyRunning or JobErrorCodes.Busy or JobErrorCodes.CatalogUnavailable or JobErrorCodes.FileExists
            => StatusCodes.Status409Conflict,
        JobErrorCodes.InvalidRequest => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    public static IResult List(JobRegistry jobs) =>
        Results.Json(new JobsResponse { Jobs = jobs.GetJobs(), MaxConcurrent = jobs.MaxConcurrent },
            AppCoreJsonContext.Default.JobsResponse);

    /// <summary>
    /// <c>GET /jobs/events</c> (SSE): a <c>snapshot</c> event (<see cref="JobsResponse"/>), then one <c>job</c> event
    /// (<see cref="JobDto"/>) per change until the client goes away.
    /// </summary>
    public static async Task EventsAsync(HttpContext ctx, JobRegistry jobs)
    {
        using var subscription = jobs.Subscribe();
        Sse.Start(ctx.Response);
        using var sse = new SseWriter(ctx.Response.BodyWriter);
        try
        {
            sse.Write("snapshot"u8, new JobsResponse { Jobs = subscription.Snapshot, MaxConcurrent = jobs.MaxConcurrent },
                AppCoreJsonContext.Default.JobsResponse);
            if (!await sse.FlushAsync(ctx.RequestAborted)) return;
            await sse.DrainAsync(subscription.Changes,
                (w, job) => w.Write("job"u8, job, AppCoreJsonContext.Default.JobDto), ctx.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // The client went away.
        }
    }

    public static IResult Cancel(long id, JobRegistry jobs) =>
        jobs.Cancel(id) ? Results.NoContent() : Results.NotFound();

    public static IResult Dismiss(long id, JobRegistry jobs) => jobs.Dismiss(id) switch
    {
        DismissOutcome.Removed => Results.NoContent(),
        DismissOutcome.NotFinished => Results.Conflict(),
        _ => Results.NotFound()
    };

    public static IResult DismissFinished(JobRegistry jobs)
    {
        jobs.DismissFinished();
        return Results.NoContent();
    }
}
