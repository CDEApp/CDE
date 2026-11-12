using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace cdeAppCore.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter<JobKind>))]
public enum JobKind
{
    [JsonStringEnumMemberName("rescan")] Rescan,
    [JsonStringEnumMemberName("create")] Create,
    [JsonStringEnumMemberName("hash")] Hash
}

/// <summary>queued -> running -> succeeded | failed | cancelled, and queued -> cancelled. Terminal states are final.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobState>))]
public enum JobState
{
    [JsonStringEnumMemberName("queued")] Queued,
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("succeeded")] Succeeded,
    [JsonStringEnumMemberName("failed")] Failed,
    [JsonStringEnumMemberName("cancelled")] Cancelled
}

public sealed record JobDto
{
    public long Id { get; init; }
    public JobKind Kind { get; init; }
    public string Title { get; init; }

    /// <summary>Rescan: the catalog. Create: null until it succeeds, then the new catalog's id. Hash: null.</summary>
    public int? CatalogId { get; init; }

    /// <summary>Hash: the catalogs the run hashes together, in ascending order. Null for the other kinds.</summary>
    public int[] CatalogIds { get; init; }

    /// <summary>The catalog file name without extension. Hash: the names of its catalogs, joined with ", ".</summary>
    public string CatalogName { get; init; }

    public JobState State { get; init; }
    public DateTime QueuedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }

    /// <summary>
    /// <c>queued</c>, then the scan's phase: <c>scanning</c>, <c>writing</c>, <c>swapping</c> (rescan) or <c>saving</c>
    /// (create). Hash: <c>hashing</c>, <c>hashing-full</c>, <c>saving</c>, <c>swapping</c>.
    /// </summary>
    public string Phase { get; init; }

    /// <summary>The highest count reported so far; later phases report 0 and do not lower it.</summary>
    public long EntriesScanned { get; init; }

    /// <summary>Hash: the counts of the current pass, from the first report on. Null for the other kinds.</summary>
    public HashJobProgress? Hash { get; init; }

    public string Note { get; init; }
    public JobResult Result { get; init; }
    public JobError Error { get; init; }

    [JsonIgnore]
    public bool IsTerminal => State is JobState.Succeeded or JobState.Failed or JobState.Cancelled;

    public JobDto Start(DateTime nowUtc) =>
        MoveTo(JobState.Running) with { StartedAtUtc = nowUtc, Phase = Kind == JobKind.Hash ? "hashing" : "scanning" };

    public JobDto Succeed(JobResult result, DateTime nowUtc) =>
        MoveTo(JobState.Succeeded) with { EndedAtUtc = nowUtc, Result = result, CatalogId = result.CatalogId };

    public JobDto Fail(JobError error, DateTime nowUtc) =>
        MoveTo(JobState.Failed) with { EndedAtUtc = nowUtc, Error = error };

    public JobDto Cancel(DateTime nowUtc) =>
        MoveTo(JobState.Cancelled) with { EndedAtUtc = nowUtc };

    private JobDto MoveTo(JobState next)
    {
        var allowed = (State, next) is (JobState.Queued, JobState.Running or JobState.Cancelled)
            or (JobState.Running, JobState.Succeeded or JobState.Failed or JobState.Cancelled);
        if (!allowed) throw new InvalidOperationException($"Job {Id} cannot move from {State} to {next}.");
        return this with { State = next };
    }
}

public sealed class JobResult
{
    /// <summary>The catalog a rescan or create wrote. Null for a hash, which names its catalogs in <see cref="JobDto.CatalogIds"/>.</summary>
    public int? CatalogId { get; set; }
    public string RootPath { get; set; }
    public long Entries { get; set; }
    public string SavedPath { get; set; }
    public int ExcludedFolderCount { get; set; }
    public int AccessDeniedFolderCount { get; set; }
    public int UnreadableFolderCount { get; set; }

    /// <summary>Hash: what the run did. Null for the other kinds, whose fields above a hash leaves empty.</summary>
    public HashResult Hash { get; set; }
}

/// <summary>Why a job failed, or why a submit was refused. <see cref="Code"/> is one of <see cref="JobErrorCodes"/>.</summary>
public sealed class JobError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string RootPath { get; set; }
}

public static class JobErrorCodes
{
    public const string InvalidRequest = "invalid-request";
    public const string UnknownCatalog = "unknown-catalog";
    public const string AlreadyRunning = "already-running";
    public const string Busy = "busy";
    public const string CatalogUnavailable = "catalog-unavailable";
    public const string FileExists = "file-exists";
    public const string RootNotAccessible = "root-not-accessible";
    public const string NoSourceFile = "no-source-file";
    public const string InvalidFileName = "invalid-file-name";
    public const string NoCatalogDir = "no-catalog-dir";
    public const string FolderNotWritable = "folder-not-writable";
    public const string RescanFailed = "rescan-failed";
    public const string CreateFailed = "create-failed";

    /// <summary>The hash filter is not a valid query; the message is the one the search bar shows.</summary>
    public const string InvalidFilter = "invalid-filter";

    /// <summary>A legacy <c>.cde</c> catalog cannot carry XxHash128 hashes; migrate it first.</summary>
    public const string LegacyCatalog = "legacy-catalog";
    public const string HashFailed = "hash-failed";
}

/// <summary>
/// Body of <c>POST /jobs</c>. <c>rescan</c> uses <see cref="CatalogId"/>; <c>create</c> uses <see cref="RootPath"/>,
/// <see cref="FileName"/> (no extension; empty means the default name), <see cref="Description"/> and
/// <see cref="FollowJunctions"/>; <c>hash</c> uses <see cref="CatalogIds"/> and <see cref="Hash"/>.
/// </summary>
public sealed class JobSubmitRequest
{
    public JobKind? Kind { get; set; }
    public int? CatalogId { get; set; }

    /// <summary>
    /// <c>hash</c>: the catalogs to hash together, one or many. File sizes are compared across the whole set, so a
    /// file whose only same-size match is in another catalog of the set is hashed too.
    /// </summary>
    public int[] CatalogIds { get; set; }

    /// <summary><c>hash</c>: which files to hash; null hashes every file with a repeated size.</summary>
    public HashOptions Hash { get; set; }
    public string RootPath { get; set; }
    public string FileName { get; set; }
    public string Description { get; set; }
    public bool FollowJunctions { get; set; }
}

/// <summary><c>GET /jobs</c>, and the first <c>snapshot</c> event of <c>GET /jobs/events</c>.</summary>
public sealed class JobsResponse
{
    public IReadOnlyList<JobDto> Jobs { get; set; } = [];
    public int MaxConcurrent { get; set; }
}
