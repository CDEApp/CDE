using System;
using System.IO;

namespace cdeLib.Entities.Columnar;

public static partial class ColumnarFormat
{
    /// <summary>
    /// Writes a copy of <paramref name="source"/> with a new description. Only the metadata column changes:
    /// every other column and the header flags (including the hash kind) are copied as they are, so a
    /// catalog with legacy hashes stays legacy. Writes straight to <paramref name="outPath"/>; the caller owns
    /// any temp file and swap (the source file is still mapped while this runs).
    /// </summary>
    public static void WriteWithDescription(ColumnarCatalogReader source, string description, string outPath)
    {
        ArgumentNullException.ThrowIfNull(source);

        var meta = BuildMeta(
            source.RootPath, source.VolumeName, source.DefaultFileName, source.ActualFileName, source.DriveLetterHint,
            description, source.AvailSpace, source.TotalSpace, source.ScanStartUtcTicks, source.ScanEndUtcTicks,
            source.RootSize, source.RootFileEntryCount, source.RootDirEntryCount);

        var len = new long[ColumnCount];
        for (var c = 0; c < ColumnCount; c++) len[c] = source.ColumnBytes((Col)c).Length;
        len[(int)Col.Meta] = meta.Length;

        var flags = (source.Hashes == CatalogHashes.None ? 0 : FlagHasHashes)
                    | (source.Hashes == CatalogHashes.XxHash128 ? FlagHashXxHash128 : 0)
                    | (source.FollowJunctions ? FlagFollowJunctions : 0);

        var off = LayOut(len, out var pos);
        var header = BuildHeader(source.Count, flags, off, len);

        using var handle = File.OpenHandle(outPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
        RandomAccess.SetLength(handle, pos); // gap bytes between columns stay zero
        RandomAccess.Write(handle, header, 0);
        for (var c = 0; c < ColumnCount; c++)
        {
            var col = (Col)c;
            RandomAccess.Write(handle, col == Col.Meta ? meta : source.ColumnBytes(col), off[c]);
        }
    }
}
