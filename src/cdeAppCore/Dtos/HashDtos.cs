using System;

namespace cdeAppCore.Dtos;

/// <summary>Which files a hash run reads (<c>cdeLib.Hashing.HashSelection</c>). It belongs to one run and is never saved.</summary>
public sealed class HashOptions
{
    /// <summary>An Everything-style query a file must match, in the search bar's syntax; blank selects every file.</summary>
    public string Filter { get; set; }

    /// <summary>Also hash files whose size no other file in the run's catalogs has; each selected file then gets a full hash.</summary>
    public bool IncludeUniqueSizes { get; set; }
}

/// <summary>
/// Body of <c>POST /catalogs/hash/preflight</c>: the same catalog set and options as a <c>hash</c> job
/// (<see cref="JobSubmitRequest.CatalogIds"/>, <see cref="JobSubmitRequest.Hash"/>).
/// </summary>
public sealed class HashPreflightRequest
{
    public int[] CatalogIds { get; set; }
    public HashOptions Hash { get; set; }
}

/// <summary>What a hash run with the same catalogs and options would read, counted without reading any file.</summary>
public sealed class HashPreflightResult
{
    /// <summary>Selected files the run reads in its first pass: those with no usable hash (with
    /// <see cref="HashOptions.IncludeUniqueSizes"/>, also those with only a partial hash).</summary>
    public long Files { get; set; }

    /// <summary>The total size of those files. The first pass reads only the start of each; possible duplicates are then read whole.</summary>
    public long Bytes { get; set; }

    /// <summary>Selected files whose hash the run keeps as it is.</summary>
    public long AlreadyHashed { get; set; }
}

/// <summary>
/// The current pass of a running hash job. The counts start again at 0 when <see cref="JobDto.Phase"/> goes from
/// <c>hashing</c> (first pass) to <c>hashing-full</c>. A file counts when it has been read or has failed, and
/// <see cref="BytesDone"/> grows by the file's planned bytes at that moment, not while the file is read.
/// </summary>
public readonly record struct HashJobProgress(long FilesDone, long FilesTotal, long BytesDone, long BytesTotal);

/// <summary>Hash progress. <see cref="Phase"/> is <c>hashing</c>, <c>hashing-full</c>, <c>saving</c> or <c>swapping</c>.</summary>
public readonly record struct HashProgress(string Phase, HashJobProgress Counts, string Note);

/// <summary>The outcome of a hash run that ended without a failure.</summary>
public sealed class HashResult
{
    /// <summary>Files given a new or upgraded hash.</summary>
    public long FilesHashed { get; set; }

    /// <summary>Files that could not be read (locked, gone, access denied); they stay unhashed.</summary>
    public long FilesFailed { get; set; }
}

/// <summary>A hash run that cannot start. <see cref="Error"/> carries one of <see cref="JobErrorCodes"/>.</summary>
public sealed class HashRefusedException(JobError error) : Exception(error.Message)
{
    public JobError Error { get; } = error;
}
