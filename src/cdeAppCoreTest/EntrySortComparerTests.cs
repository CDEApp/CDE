using System;
using cdeAppCore.Sorting;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class EntrySortComparerTests
{
    private static RootEntry BuildRoot(string path, string volume, string actualName, string drive, string description,
        long avail, long total, long scanStart, long scanEnd, int fileCount)
    {
        var root = new RootEntry
        {
            Path = path,
            VolumeName = volume,
            DefaultFileName = actualName,
            ActualFileName = actualName,
            DriveLetterHint = drive,
            Description = description,
            AvailSpace = avail,
            TotalSpace = total,
            ScanStartUtcTicks = scanStart,
            ScanEndUtcTicks = scanEnd,
        };

        root.AddChild(new DirEntry(true) { Path = "dir" });
        if (fileCount > 2)
        {
            root.AddChild(new DirEntry(true) { Path = "dir2" });
        }

        for (var i = 0; i < fileCount; i++)
        {
            root.AddChild(new DirEntry(false) { Path = $"{volume}{i}.txt", Size = 100 * (i + 1) });
        }

        root.SetInMemoryFields();
        return root;
    }

    private static ICommonEntry RootRef(RootEntry root) => new EntryRef(EntryStore.Build(root), 0);

    private static (ICommonEntry A, ICommonEntry B) TwoCatalogs()
    {
        var a = BuildRoot(@"C:\a", "AAA", "a.cdex", "C", "alpha", 10, 100, 1000, 1010, 1);
        var b = BuildRoot(@"D:\b", "BBB", "b.cdex", "D", "beta", 20, 200, 2000, 2020, 3);
        return (RootRef(a), RootRef(b));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(12)]
    public void CompareCatalog_EveryColumn_OrdersAscendingAndReversesWhenDescending(int column)
    {
        var (a, b) = TwoCatalogs();

        var asc = EntrySortComparer.CompareCatalog(a, b, column, false);
        var desc = EntrySortComparer.CompareCatalog(a, b, column, true);

        Assert.That(asc, Is.Not.Zero, $"column {column} should distinguish the two catalogs");
        Assert.That(Math.Sign(desc), Is.EqualTo(-Math.Sign(asc)));
        Assert.That(EntrySortComparer.CompareCatalog(a, a, column, false), Is.Zero);
    }

    [Test]
    public void CompareCatalog_NullVolumeName_SortsAsEmpty()
    {
        var withVolume = RootRef(BuildRoot(@"C:\a", "AAA", "a.cdex", "C", "x", 1, 1, 1, 2, 1));
        var noVolume = RootRef(BuildRoot(@"C:\a", null, "a.cdex", "C", "x", 1, 1, 1, 2, 1));

        Assert.That(EntrySortComparer.CompareCatalog(noVolume, withVolume, 1, false), Is.LessThan(0));
    }

    [Test]
    public void CompareCatalog_UnknownColumn_Throws()
    {
        var (a, b) = TwoCatalogs();
        Assert.Throws<Exception>(() => EntrySortComparer.CompareCatalog(a, b, 99, false));
    }

    [Test]
    public void CompareDirectory_ByNameSizeAndModified()
    {
        var older = new DirEntry(false) { Path = "a.txt", Size = 10, Modified = new DateTime(2020, 1, 1) };
        var newer = new DirEntry(false) { Path = "b.txt", Size = 20, Modified = new DateTime(2021, 1, 1) };

        Assert.That(EntrySortComparer.CompareDirectory(older, newer, 0, false), Is.LessThan(0));
        Assert.That(EntrySortComparer.CompareDirectory(older, newer, 1, false), Is.LessThan(0));
        Assert.That(EntrySortComparer.CompareDirectory(older, newer, 2, false), Is.LessThan(0));
        Assert.That(EntrySortComparer.CompareDirectory(older, newer, 2, true), Is.GreaterThan(0));
    }

    [Test]
    public void CompareDirectory_UnknownColumn_Throws()
    {
        var e = new DirEntry(false) { Path = "a.txt" };
        Assert.Throws<Exception>(() => EntrySortComparer.CompareDirectory(e, e, 3, false));
    }

    private static (PairDirEntry First, PairDirEntry Second) TwoPairs(bool useEntryRefs)
    {
        var a = BuildRoot(@"C:\a", "AAA", "a.cdex", "C", "alpha", 10, 100, 1000, 1010, 1);
        var b = BuildRoot(@"D:\b", "BBB", "b.cdex", "D", "beta", 20, 200, 2000, 2020, 3);

        if (!useEntryRefs)
        {
            return (new PairDirEntry(a, a.Children[1]), new PairDirEntry(b, b.Children[1]));
        }

        var ra = RootRef(a);
        var rb = RootRef(b);
        return (new PairDirEntry(ra, ra.Children[1]), new PairDirEntry(rb, rb.Children[1]));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(4)]
    public void CompareSearchResult_Columns_OrderAndReverse(int column)
    {
        var (first, second) = TwoPairs(useEntryRefs: true);

        var asc = EntrySortComparer.CompareSearchResult(first, second, column, false);
        var desc = EntrySortComparer.CompareSearchResult(first, second, column, true);

        Assert.That(asc, Is.Not.Zero);
        Assert.That(Math.Sign(desc), Is.EqualTo(-Math.Sign(asc)));
    }

    [Test]
    public void CompareSearchResult_SizeAndModifiedColumns_CompareChildEntries()
    {
        var small = new DirEntry(false) { Path = "a.txt", Size = 1, Modified = new DateTime(2020, 1, 1) };
        var large = new DirEntry(false) { Path = "a.txt", Size = 9, Modified = new DateTime(2021, 1, 1) };
        var root = BuildRoot(@"C:\a", "AAA", "a.cdex", "C", "alpha", 1, 1, 1, 2, 0);
        var first = new PairDirEntry(root, small);
        var second = new PairDirEntry(root, large);

        Assert.That(EntrySortComparer.CompareSearchResult(first, second, 1, false), Is.LessThan(0));
        Assert.That(EntrySortComparer.CompareSearchResult(first, second, 2, false), Is.LessThan(0));
    }

    [Test]
    public void CompareSearchResult_CatalogColumn_FallsBackToRootEntryForInMemoryPairs()
    {
        var (first, second) = TwoPairs(useEntryRefs: false);

        Assert.That(EntrySortComparer.CompareSearchResult(first, second, 3, false), Is.LessThan(0));
    }

    [Test]
    public void CompareSearchResult_PathColumn_TiesBreakOnChildPath()
    {
        var root = BuildRoot(@"C:\a", "AAA", "a.cdex", "C", "alpha", 1, 1, 1, 2, 2);
        var first = new PairDirEntry(root, root.Children[1]);
        var second = new PairDirEntry(root, root.Children[2]);

        Assert.That(EntrySortComparer.CompareSearchResult(first, second, 4, false), Is.LessThan(0));
    }

    [Test]
    public void CompareSearchResult_UnknownColumn_Throws()
    {
        var (first, second) = TwoPairs(useEntryRefs: false);
        Assert.Throws<Exception>(() => EntrySortComparer.CompareSearchResult(first, second, 99, false));
    }
}
