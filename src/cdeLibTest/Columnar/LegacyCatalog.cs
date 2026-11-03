using System;
using System.Buffers.Binary;
using System.IO;
using cdeLib.Entities.Columnar;

namespace cdeLibTest.Columnar;

internal static class LegacyCatalog
{
    public static void MarkPre222(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        Span<byte> word = stackalloc byte[4];
        file.Position = 12;
        file.ReadExactly(word);
        var flags = BinaryPrimitives.ReadInt32LittleEndian(word) & ~ColumnarFormat.FlagHashXxHash128;
        BinaryPrimitives.WriteInt32LittleEndian(word, flags);
        file.Position = 12;
        file.Write(word);
    }
}
