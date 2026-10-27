using System.Threading.Tasks;
using cdeLib.Duplicates;
using NUnit.Framework;

namespace cdeLibTest.Duplicates;

[TestFixture]
public class DuplicationStatisticsTests
{
    [Test]
    public void Counters_StartAtZero()
    {
        var s = new DuplicationStatistics();

        Assert.That(s.FilesProcessed, Is.Zero);
        Assert.That(s.LargestFileSize, Is.Zero);
        Assert.That(s.FilesToCheckForDuplicatesCount, Is.Zero);
    }

    [Test]
    public void Adders_IncrementTheirCountersAndFilesProcessed()
    {
        var s = new DuplicationStatistics();

        s.AddPartialHash();
        s.AddFullHash();
        s.AddFailedToHash();
        s.AddAllreadyDonePartial();
        s.AddAllreadyDoneFull();
        s.AddListOfDuplicatesProcessed();

        Assert.That(s.PartialHashes, Is.EqualTo(1));
        Assert.That(s.FullHashes, Is.EqualTo(1));
        Assert.That(s.FailedToHash, Is.EqualTo(1));
        Assert.That(s.AllreadyDonePartials, Is.EqualTo(1));
        Assert.That(s.AllreadyDoneFulls, Is.EqualTo(1));
        Assert.That(s.ListOfDuplicatesProcessed, Is.EqualTo(1));
        Assert.That(s.FilesProcessed, Is.EqualTo(5), "the list-of-duplicates counter is not a file");
    }

    [Test]
    public void ByteCounters_Accumulate()
    {
        var s = new DuplicationStatistics();

        s.AddBytesProcessed(10);
        s.AddBytesProcessed(5);
        s.AddTotalFileBytes(100);
        s.AddBytesNotProcessed(7);
        s.FilesToCheckForDuplicatesCount = 42;

        Assert.That(s.BytesProcessed, Is.EqualTo(15));
        Assert.That(s.TotalFileBytes, Is.EqualTo(100));
        Assert.That(s.BytesNotProcessed, Is.EqualTo(7));
        Assert.That(s.FilesToCheckForDuplicatesCount, Is.EqualTo(42));
    }

    [Test]
    public void SeenFileSize_TracksLargestAndSmallest()
    {
        var s = new DuplicationStatistics();

        foreach (var size in new long[] { 50, 10, 200, 75 })
        {
            s.SeenFileSize(size);
        }

        Assert.That(s.LargestFileSize, Is.EqualTo(200));
        Assert.That(s.SmallestFileSize, Is.EqualTo(10));
    }

    [Test]
    public void SeenFileSize_ConcurrentCallers_KeepTrueExtremes()
    {
        var s = new DuplicationStatistics();

        Parallel.For(1, 2001, i => s.SeenFileSize(i));

        Assert.That(s.LargestFileSize, Is.EqualTo(2000));
        Assert.That(s.SmallestFileSize, Is.EqualTo(1));
    }
}
