using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;

namespace cdeLib;

// What a node is tested against: one text, or an entry that gives its name, path and properties.
public sealed partial class EverythingQuery
{
    // Either one text standing for both name and path, or an entry giving them separately. A plain struct passed to
    // a plain virtual Eval: a generic virtual method here was measurably slower on the hot scan loop.
    private readonly ref struct Context
    {
        public readonly ReadOnlySpan<byte> Bytes;
        public readonly ReadOnlySpan<char> Chars;
        public readonly IEntryText Utf8Entry;
        public readonly IEntryChars CharEntry;
        public readonly bool DefaultMatchesPath;
        public readonly bool IsFolder; // only for the single-text sources; an entry answers for itself
        public readonly long Size; // likewise
        public readonly long ModifiedTicks; // likewise
        public readonly EntryCounts Counts; // likewise
        public readonly ulong Duplicates; // likewise
        public readonly Hash16? FullHash;
        public readonly Kind Source;

        public Context(ReadOnlySpan<byte> bytes, bool isFolder, long size, long modifiedTicks, EntryCounts counts,
            ulong duplicates, Hash16? fullHash)
        {
            Bytes = bytes;
            IsFolder = isFolder;
            Size = size;
            ModifiedTicks = modifiedTicks;
            Counts = counts;
            Duplicates = duplicates;
            FullHash = fullHash;
            Source = Kind.SingleBytes;
        }

        public Context(ReadOnlySpan<char> chars, bool isFolder, long size, long modifiedTicks, EntryCounts counts,
            ulong duplicates, Hash16? fullHash)
        {
            Chars = chars;
            IsFolder = isFolder;
            Size = size;
            ModifiedTicks = modifiedTicks;
            Counts = counts;
            Duplicates = duplicates;
            FullHash = fullHash;
            Source = Kind.SingleChars;
        }

        public Context(IEntryText entry, bool defaultMatchesPath)
        {
            Utf8Entry = entry;
            DefaultMatchesPath = defaultMatchesPath;
            Source = Kind.Utf8Entry;
        }

        public Context(IEntryChars entry, bool defaultMatchesPath)
        {
            CharEntry = entry;
            DefaultMatchesPath = defaultMatchesPath;
            Source = Kind.CharEntry;
        }
    }

    private enum Kind { SingleBytes, SingleChars, Utf8Entry, CharEntry }

    private sealed class StringEntry(string name, string path, bool isFolder, long size, long modifiedTicks,
        EntryCounts counts, ulong duplicates, Hash16? fullHash)
        : IEntryChars
    {
        public ReadOnlySpan<char> Name => name;
        public ReadOnlySpan<char> Path => path;
        public bool IsFolder => isFolder;
        public long Size => size;
        public long ModifiedTicks => modifiedTicks;
        public EntryCounts Counts => counts;
        public ulong Duplicates => duplicates;
        public Hash16? FullHash => fullHash;
    }
}
