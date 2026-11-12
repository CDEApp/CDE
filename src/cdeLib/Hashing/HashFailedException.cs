using System;

namespace cdeLib.Hashing;

/// <summary>
/// A hash run did not complete: a hash phase stopped on an unexpected error, or a hashed catalog could not be
/// saved. Hashes computed before the failure were still saved where possible. <see cref="Exception.Message"/> is
/// the one-line summary shown to the user; <c>cde</c> exits non-zero for it.
/// </summary>
public sealed class HashFailedException : Exception
{
    public HashFailedException(string summary, Exception inner, long filesSaved = 0, long filesFailed = 0)
        : base(summary, inner)
    {
        FilesSaved = filesSaved;
        FilesFailed = filesFailed;
    }

    /// <summary>Files that got a new usable hash in a catalog that was saved before the run failed.</summary>
    public long FilesSaved { get; }

    public long FilesFailed { get; }

    /// <summary>The summary line, e.g. "Hash FAILED: 12 files hashed, 1 file could not be read; unexpected error: boom".</summary>
    public static string Summarize(long filesHashed, long filesFailed, string reason) =>
        $"Hash FAILED: {filesHashed} {(filesHashed == 1 ? "file" : "files")} hashed, " +
        $"{filesFailed} {(filesFailed == 1 ? "file" : "files")} could not be read; {reason}";
}
