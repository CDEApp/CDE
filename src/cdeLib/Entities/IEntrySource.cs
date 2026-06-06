using System;
using System.Collections.Generic;
using System.Text;
using cdeLib.Entities.Soa;

namespace cdeLib.Entities;

/// <summary>
/// A read-only, index-addressed catalog: the common surface over both the in-memory
/// <see cref="EntryStore"/> (built from a loaded <c>.cde</c> tree) and the zero-copy
/// <see cref="Columnar.ColumnarCatalogReader"/> (mmap over a <c>.cdex</c> file). Lets the GUI and
/// <see cref="EntryRef"/> present and search a catalog without caring whether it lives on the managed
/// heap or in a memory map. Index 0 is always the catalog root; child/sibling/parent chains terminate
/// at <see cref="EntryStore.None"/>.
/// </summary>
public interface IEntrySource
{
    int Count { get; }

    // ----- per-entry accessors -----
    long SizeOf(int i);
    DateTime ModifiedOf(int i);
    Flags FlagsOf(int i);
    bool IsDirectory(int i);
    bool IsHashDone(int i);
    bool IsPartialHash(int i);
    bool HasHash { get; }
    Hash16 HashOf(int i);

    string FullName(int i);   // name + extension
    string NameOf(int i);     // name component used for path-problem checks (== FullName when not split)
    string FullPath(int i);
    void AppendFullPath(StringBuilder sb, int i);

    int ParentOf(int i);
    int FirstChildOf(int i);
    IEnumerable<int> ChildrenOf(int i);

    // ----- full-filter search (pattern + name/path + file/folder + size/date/hour) -----
    void Find(EntryStoreFindOptions options, Action<int> onMatch,
        Func<bool> isCancelled = null, Action<int> onScan = null);

    // ----- catalog-level metadata (what the GUI catalog list + result rows display) -----
    string RootPath { get; }
    string VolumeName { get; }
    string DefaultFileName { get; }
    string ActualFileName { get; }
    string DriveLetterHint { get; }
    string Description { get; }
    long AvailSpace { get; }
    long TotalSpace { get; }
    long ScanStartUtcTicks { get; }
    long ScanEndUtcTicks { get; }
    long RootSize { get; }
    uint RootFileEntryCount { get; }
    uint RootDirEntryCount { get; }
}
