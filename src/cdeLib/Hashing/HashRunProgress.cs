namespace cdeLib.Hashing;

/// <summary>
/// One pass of a hash run: the first pass reads the start of each file with no usable hash, the full pass reads each
/// possible duplicate to its end. <paramref name="BytesDone"/> grows as each file ends, not while it is read.
/// </summary>
public readonly record struct HashRunProgress(bool FullPass, long FilesDone, long FilesTotal, long BytesDone,
    long BytesTotal);
