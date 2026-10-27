using System;
using System.Collections.Generic;
using cdeLib.Entities;
using FlatSharp;

namespace cdeLib.Catalog.FlatBuffers;

/// <summary>
/// FlatBuffers protocol for the legacy .cde tree format. FlatSharp 7 only serializes the types it
/// generates from Catalog.fbs, so the RootEntry/DirEntry tree is mapped to and from those types.
/// </summary>
internal static class FlatBufferCatalogSerializer
{
    public static ReadOnlyMemory<byte> Serialize(RootEntry rootEntry)
    {
        var fbRoot = ToFlatBuffer(rootEntry);
        var buffer = new byte[FbRootEntry.Serializer.GetMaxSize(fbRoot)];
        var bytesWritten = FbRootEntry.Serializer.Write(buffer, fbRoot);
        return buffer.AsMemory(0, bytesWritten);
    }

    public static RootEntry Deserialize(ReadOnlyMemory<byte> bytes)
    {
        return ToRootEntry(FbRootEntry.Serializer.Parse(bytes));
    }

    private static FbRootEntry ToFlatBuffer(RootEntry rootEntry)
    {
        return new FbRootEntry
        {
            Description = rootEntry.Description,
            PathsWithUnauthorisedExceptions = rootEntry.PathsWithUnauthorisedExceptions,
            DefaultFileName = rootEntry.DefaultFileName,
            DriveLetterHint = rootEntry.DriveLetterHint,
            AvailSpace = rootEntry.AvailSpace,
            TotalSpace = rootEntry.TotalSpace,
            ScanStartUtcTicks = rootEntry.ScanStartUtcTicks,
            ScanEndUtcTicks = rootEntry.ScanEndUtcTicks,
            Version = rootEntry.Version,
            BitFields = (byte)rootEntry.BitFields,
            Hash = ToFlatBuffer(rootEntry.Hash),
            ModifiedTicks = rootEntry.ModifiedTicks,
            Children = ToFlatBuffer(rootEntry.Children),
            Size = rootEntry.Size,
            Path = rootEntry.Path,
            VolumeName = rootEntry.VolumeName,
        };
    }

    private static List<FbDirEntry> ToFlatBuffer(IList<DirEntry> children)
    {
        if (children == null) return null;

        var fbChildren = new List<FbDirEntry>(children.Count);
        foreach (var child in children)
        {
            fbChildren.Add(new FbDirEntry
            {
                ModifiedTicks = child.ModifiedTicks,
                Hash = ToFlatBuffer(child.Hash),
                Children = ToFlatBuffer(child.Children),
                Size = child.Size,
                Path = child.Path,
                BitFields = (byte)child.BitFields,
            });
        }

        return fbChildren;
    }

    // An unset hash is all zeros; leave the field out rather than write 16 zero bytes.
    private static FbHash16? ToFlatBuffer(Hash16 hash) =>
        hash.IsSet ? new FbHash16 { HashA = hash.HashA, HashB = hash.HashB } : null;

    private static RootEntry ToRootEntry(FbRootEntry fbRoot)
    {
        var fbPaths = fbRoot.PathsWithUnauthorisedExceptions;
        return new RootEntry
        {
            Description = fbRoot.Description,
            PathsWithUnauthorisedExceptions = fbPaths == null ? null : new List<string>(fbPaths),
            DefaultFileName = fbRoot.DefaultFileName,
            DriveLetterHint = fbRoot.DriveLetterHint,
            AvailSpace = fbRoot.AvailSpace,
            TotalSpace = fbRoot.TotalSpace,
            ScanStartUtcTicks = fbRoot.ScanStartUtcTicks,
            ScanEndUtcTicks = fbRoot.ScanEndUtcTicks,
            Version = fbRoot.Version,
            BitFields = (Flags)fbRoot.BitFields,
            Hash = ToHash16(fbRoot.Hash),
            ModifiedTicks = fbRoot.ModifiedTicks,
            Children = ToDirEntries(fbRoot.Children),
            Size = fbRoot.Size,
            Path = fbRoot.Path,
            VolumeName = fbRoot.VolumeName,
        };
    }

    // Lazy parsing builds a new object on every access, so each vector and field is read once.
    private static List<DirEntry> ToDirEntries(IList<FbDirEntry> fbChildren)
    {
        if (fbChildren == null) return null;

        var count = fbChildren.Count;
        var children = new List<DirEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var fbChild = fbChildren[i];
            children.Add(new DirEntry
            {
                ModifiedTicks = fbChild.ModifiedTicks,
                Hash = ToHash16(fbChild.Hash),
                Children = ToDirEntries(fbChild.Children),
                Size = fbChild.Size,
                Path = fbChild.Path,
                BitFields = (Flags)fbChild.BitFields,
            });
        }

        return children;
    }

    private static Hash16 ToHash16(FbHash16? fbHash) =>
        fbHash is { } h ? new Hash16(h.HashA, h.HashB) : Hash16.Empty;
}
