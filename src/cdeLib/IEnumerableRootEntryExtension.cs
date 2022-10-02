using System.Collections.Generic;
using System.Linq;
using cdeLib.Entities;

namespace cdeLib;

// ReSharper disable InconsistentNaming
public static class IEnumerableRootEntryExtension
{
    public static int TotalFileEntries(this IEnumerable<RootEntry> rootEntries)
    {
        return rootEntries?.Sum(rootEntry => (int)rootEntry.DirEntryCount + (int)rootEntry.FileEntryCount) ?? 0;
    }
}
// ReSharper restore InconsistentNaming