using System;
using System.Globalization;
using cdeLib.Entities;

namespace cdeLib.Output;

/// <summary>
/// The columns of the records <c>find</c>, <c>dupes</c> and <c>hash</c> write with <c>--json</c> / <c>--csv</c> (#173),
/// and the code that writes them, so the three commands name, order and format a field the same way.
/// <c>catalog</c> is the catalog file the entry is in; <c>path</c> its full path; <c>name</c> the entry's own name, as the catalog holds it (a name may hold a backslash on Linux and macOS, so it is not parsed from the path);
/// <c>modified</c> UTC; <c>hash</c> 32 lower-case hex digits (the two halves of the 16-byte hash).
/// </summary>
public static class EntryRecords
{
    public static readonly string[] FindColumns = ["catalog", "path", "name", "size", "modified", "directory"];

    public static readonly string[] DupesColumns = ["group", "catalog", "path", "name", "size", "modified", "hash"];

    public static readonly string[] HashColumns = ["catalog", "path", "name", "size", "hash", "partial"];

    /// <summary>One match of a search. Allocates only the path, as the default output does.</summary>
    public static void WriteFind(IRecordWriter writer, string catalog, IEntrySource source, int index)
    {
        var path = source.FullPath(index);
        writer.BeginRecord();
        writer.Field("catalog", catalog);
        writer.Field("path", path);
        writer.Field("name", source.FullName(index));
        writer.Field("size", source.SizeOf(index));
        writer.Field("modified", source.ModifiedOf(index));
        writer.Field("directory", source.IsDirectory(index));
        writer.EndRecord();
    }

    /// <summary>One file of a set of duplicates; <paramref name="group"/> numbers the set, from 1.</summary>
    public static void WriteDuplicate(IRecordWriter writer, long group, string catalog, IEntrySource source, int index)
    {
        var path = source.FullPath(index);
        writer.BeginRecord();
        writer.Field("group", group);
        writer.Field("catalog", catalog);
        writer.Field("path", path);
        writer.Field("name", source.FullName(index));
        writer.Field("size", source.SizeOf(index));
        writer.Field("modified", source.ModifiedOf(index));
        WriteHash(writer, source.HashOf(index));
        writer.EndRecord();
    }

    /// <summary>One file that <c>hash</c> hashed; <paramref name="partial"/> is true when only its start was hashed.</summary>
    public static void WriteHashed(IRecordWriter writer, string catalog, IEntrySource source, int index, Hash16 hash,
        bool partial)
    {
        var path = source.FullPath(index);
        writer.BeginRecord();
        writer.Field("catalog", catalog);
        writer.Field("path", path);
        writer.Field("name", source.FullName(index));
        writer.Field("size", source.SizeOf(index));
        WriteHash(writer, hash);
        writer.Field("partial", partial);
        writer.EndRecord();
    }

    private static void WriteHash(IRecordWriter writer, Hash16 hash)
    {
        Span<char> hex = stackalloc char[32];
        hash.HashA.TryFormat(hex, out _, "x16", CultureInfo.InvariantCulture);
        hash.HashB.TryFormat(hex[16..], out _, "x16", CultureInfo.InvariantCulture);
        writer.Field("hash", hex);
    }
}
