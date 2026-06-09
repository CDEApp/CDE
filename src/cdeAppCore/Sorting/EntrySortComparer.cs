using System;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Sorting;

/// <summary>
/// Pure, UI-framework-independent column sort comparisons for the catalog, directory, and
/// search-result list views. Lifted verbatim from the WinForms presenter so the ordering is
/// identical; the only difference is that sort direction is expressed as a <paramref name="descending"/>
/// bool (instead of a WinForms <c>SortOrder</c>) and the column index is passed in rather than read
/// from a list-view helper.
/// </summary>
public static class EntrySortComparer
{
    private static IEntrySource SourceOf(ICommonEntry root) => ((EntryRef)root).Source;
    private static IEntrySource SourceOfPair(PairDirEntry pde) => (pde.ChildDE as EntryRef)?.Source;

    public static int CompareSearchResult(PairDirEntry pde1, PairDirEntry pde2, int sortColumn, bool descending)
    {
        int compareResult;
        var de1 = pde1.ChildDE;
        var de2 = pde2.ChildDE;
        switch (sortColumn)
        {
            case 0: // SearchResult ListView Name column
                compareResult = de1.PathCompareWithDirTo(de2);
                break;

            case 1: // SearchResult ListView Size column
                compareResult = de1.SizeCompareWithDirTo(de2);
                break;

            case 2: // SearchResult ListView Modified column
                compareResult = de1.ModifiedCompareTo(de2);
                break;

            case 3:
                compareResult = string.Compare(
                    SourceOfPair(pde1)?.ActualFileName ?? pde1.GetRootEntry()?.ActualFileName,
                    SourceOfPair(pde2)?.ActualFileName ?? pde2.GetRootEntry()?.ActualFileName,
                    StringComparison.OrdinalIgnoreCase);
                break;

            case 4: // SearchResult ListView Path column
                compareResult = string.Compare(pde1.ParentDE.FullPath, pde2.ParentDE.FullPath,
                    StringComparison.OrdinalIgnoreCase);
                if (compareResult == 0)
                {
                    compareResult = string.Compare(de1.Path, de2.Path, StringComparison.OrdinalIgnoreCase);
                }

                break;

            default:
                throw new Exception($"Problem column {sortColumn} not handled for sort.");
        }

        if (descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }

    public static int CompareDirectory(ICommonEntry de1, ICommonEntry de2, int column, bool descending)
    {
        var compareResult = column switch
        {
            0 => // SearchResult ListView Name column
                de1.PathCompareWithDirTo(de2),
            1 => // SearchResult ListView Size column
                de1.SizeCompareWithDirTo(de2),
            2 => // SearchResult ListView Modified column
                de1.ModifiedCompareTo(de2),
            _ => throw new Exception($"Problem column {column} not handled for sort.")
        };

        if (descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }

    public static int CompareCatalog(ICommonEntry root1, ICommonEntry root2, int column, bool descending)
    {
        var re1 = SourceOf(root1);
        var re2 = SourceOf(root2);
        var compareResult = column switch
        {
            0 => string.Compare(re1.RootPath, re2.RootPath, StringComparison.Ordinal),
            1 => string.Compare(string.IsNullOrEmpty(re1.VolumeName) ? "" : re1.VolumeName,
                string.IsNullOrEmpty(re2.VolumeName) ? "" : re2.VolumeName, StringComparison.Ordinal),
            2 => re1.RootDirEntryCount.CompareTo(re2.RootDirEntryCount),
            3 => re1.RootFileEntryCount.CompareTo(re2.RootFileEntryCount),
            4 => (re1.RootDirEntryCount + re1.RootFileEntryCount).CompareTo(re2.RootDirEntryCount + re2.RootFileEntryCount),
            5 => string.Compare(re1.DriveLetterHint, re2.DriveLetterHint, StringComparison.Ordinal),
            6 => re1.RootSize.CompareTo(re2.RootSize),
            7 => re1.AvailSpace.CompareTo(re2.AvailSpace),
            8 => re1.TotalSpace.CompareTo(re2.TotalSpace),
            9 => re1.ScanStartUtcTicks.CompareTo(re2.ScanStartUtcTicks),
            10 => (re1.ScanEndUtcTicks - re1.ScanStartUtcTicks).CompareTo(re2.ScanEndUtcTicks - re2.ScanStartUtcTicks),
            11 => string.Compare(re1.ActualFileName, re2.ActualFileName, StringComparison.Ordinal),
            12 => string.Compare(re1.Description, re2.Description, StringComparison.Ordinal),
            _ => throw new Exception($"Problem column {column} not handled for sort.")
        };

        if (descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }
}
