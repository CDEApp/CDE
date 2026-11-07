namespace cdeLib.Hashing;

/// <summary>The work of a hash run, from <see cref="ColumnarCatalogHashing.Preview"/>.</summary>
/// <param name="Files">Selected files the run will read: those with no usable hash, and with
/// <see cref="HashSelection.IncludeUniqueSizes"/> those with only a partial hash.</param>
/// <param name="Bytes">The total size of those files.</param>
/// <param name="AlreadyHashed">Selected files whose hash the run keeps as it is.</param>
public readonly record struct HashPreview(long Files, long Bytes, long AlreadyHashed);
