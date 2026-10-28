using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Extensions;
using NUnit.Framework;

namespace cdeLibTest.Extensions;

[TestFixture]
public class ListExtensionsTests
{
    [Test]
    public void TruncateList_RemovesItemsPastMax()
    {
        IList list = new List<int> { 1, 2, 3, 4, 5 };

        list.TruncateList(2);

        Assert.That(list, Is.EqualTo(new[] { 1, 2 }));
    }

    [TestCase(5)]
    [TestCase(9)]
    public void TruncateList_ListNoLongerThanMax_IsUnchanged(int max)
    {
        IList list = new List<int> { 1, 2, 3, 4, 5 };

        list.TruncateList(max);

        Assert.That(list, Has.Count.EqualTo(5));
    }

    [Test]
    public void TruncateList_ToZero_EmptiesTheList()
    {
        IList list = new List<string> { "a", "b" };

        list.TruncateList(0);

        Assert.That(list, Is.Empty);
    }

    [Test]
    public void Sort_List_UsesTheComparison()
    {
        IList<int> list = new List<int> { 3, 1, 2 };

        list.Sort((a, b) => b.CompareTo(a));

        Assert.That(list, Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public void Sort_Array_UsesTheComparison()
    {
        IList<string> array = new[] { "pear", "fig", "apple" };

        array.Sort((a, b) => a.Length.CompareTo(b.Length));

        Assert.That(array, Is.EqualTo(new[] { "fig", "pear", "apple" }));
    }

    // Neither a List<T> nor a T[]: copied out, sorted, and copied back into the same list.
    [Test]
    public void Sort_OtherIList_IsSortedInPlace()
    {
        var inner = new List<int> { 5, 3, 9, 1 };
        IList<int> list = new ReadOnlyCollectionWrapper<int>(inner);

        list.Sort((a, b) => a.CompareTo(b));

        Assert.That(inner, Is.EqualTo(new[] { 1, 3, 5, 9 }));
    }

    [Test]
    public void Sort_EmptyList_DoesNothing()
    {
        IList<int> list = new Collection<int>();

        Assert.DoesNotThrow(() => list.Sort((a, b) => a.CompareTo(b)));
        Assert.That(list, Is.Empty);
    }

    // Collection<T> is a plain IList<T> that writes through to its inner list.
    private sealed class ReadOnlyCollectionWrapper<T>(List<T> inner) : Collection<T>(inner);
}

[TestFixture]
public class StringExtensionEdgeCaseTests
{
    [TestCase(null, @"C:\")]
    [TestCase("", @"C:\")]
    [TestCase(@"C:\a", null)]
    [TestCase(@"C:\a", "")]
    public void GetRelativePath_MissingPath_IsNull(string fullPath, string rootPath)
    {
        Assert.That(fullPath.GetRelativePath(rootPath), Is.Null);
    }

    [Test]
    public void GetRelativePath_ForwardSlashAfterRoot_IsAccepted()
    {
        Assert.That("C:/Moo/Stuff".GetRelativePath("C:/Moo"), Is.EqualTo("Stuff"));
    }

    // "C:\Mooboy" starts with "C:\Moo" but is a sibling folder, not a child.
    [Test]
    public void GetRelativePath_RootIsOnlyAPrefixOfTheNextName_IsNull()
    {
        Assert.That(@"C:\Mooboy\x".GetRelativePath(@"C:\Moo"), Is.Null);
    }

    [Test]
    public void GetRelativePath_FullPathShorterThanRootWithoutSeparator_IsNull()
    {
        Assert.That(@"C:\Mo".GetRelativePath(@"C:\Moo"), Is.Null);
    }

    [Test]
    public void GetRelativePath_RootWithTrailingSeparator_NotAPrefix_IsNull()
    {
        Assert.That(@"D:\Moo".GetRelativePath(@"C:\"), Is.Null);
    }

    [Test]
    public void GetRelativePath_FullPathEqualsRootWithSeparator_IsEmpty()
    {
        Assert.That(@"C:\".GetRelativePath(@"C:\"), Is.EqualTo(string.Empty));
    }
}

[TestFixture]
public class ParallelExtensionTests
{
    [Test]
    public void ForEachInApproximateOrder_VisitsEveryItemOnce()
    {
        var seen = new System.Collections.Concurrent.ConcurrentBag<int>();
        var source = Enumerable.Range(0, 500).AsParallel();

        source.ForEachInApproximateOrder(new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i, _) => seen.Add(i));

        Assert.That(seen.OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, 500)));
    }

    [Test]
    public void ForEachInApproximateOrder_EmptySource_DoesNotCallTheAction()
    {
        var calls = 0;

        Enumerable.Empty<int>().AsParallel()
            .ForEachInApproximateOrder(new ParallelOptions(), (_, _) => Interlocked.Increment(ref calls));

        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void ForEachInApproximateOrder_Stop_EndsTheLoopEarly()
    {
        var calls = 0;

        Enumerable.Range(0, 100_000).AsParallel().ForEachInApproximateOrder(
            new ParallelOptions { MaxDegreeOfParallelism = 1 },
            (_, state) =>
            {
                Interlocked.Increment(ref calls);
                state.Stop();
            });

        Assert.That(calls, Is.LessThan(100_000));
    }

    [Test]
    public void ForEachInApproximateOrder_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Enumerable.Range(0, 10).AsParallel().ForEachInApproximateOrder(
                new ParallelOptions { CancellationToken = cts.Token }, (_, _) => { }));
    }
}

[TestFixture]
public class FileSystemPropertiesTests
{
    [Test]
    public void GetProperties_ExistingFolder_ReportsConsistentSizes()
    {
        var props = FileSystemProperties.GetProperties(Path.GetTempPath());

        Assert.That(props.TotalBytes, Is.GreaterThan(0));
        Assert.That(props.FreeBytes, Is.GreaterThan(0));
        Assert.That(props.AvailableBytes, Is.GreaterThan(0));
        Assert.That(props.FreeBytes, Is.LessThanOrEqualTo(props.TotalBytes));
        Assert.That(props.AvailableBytes, Is.LessThanOrEqualTo(props.FreeBytes));
    }

    [Test]
    public void GetProperties_PathThatDoesNotExist_HasNoValues()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cde-no-such-folder-" + Guid.NewGuid().ToString("N"), "x");

        var props = FileSystemProperties.GetProperties(missing);

        Assert.That(props.TotalBytes, Is.Null);
        Assert.That(props.FreeBytes, Is.Null);
        Assert.That(props.AvailableBytes, Is.Null);
    }
}
