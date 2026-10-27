namespace cdeLib.Infrastructure.Hashing;

/// <summary>
/// Result of hashing one file. A value type so hashing a file allocates no response object.
/// <see cref="IsSuccess"/> is false when the file could not be opened or read.
/// </summary>
public readonly record struct HashResponse(bool IsSuccess, ulong Hash, bool IsPartialHash, long BytesHashed)
{
    public static HashResponse Failed => default;
}
