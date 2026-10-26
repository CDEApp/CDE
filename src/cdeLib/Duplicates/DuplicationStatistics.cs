using System.Threading;

namespace cdeLib.Duplicates;

/// <summary>
/// Statistics holder for <see cref="Duplication"/>. All mutations are atomic so the counters stay
/// correct when hashing runs across multiple threads (see <see cref="Duplication.ApplyHash"/>, which
/// hashes files in parallel). Plain <c>++</c>/<c>+=</c> on shared fields lose updates under
/// concurrency, which previously made the partial/full hash counts non-deterministic.
/// </summary>
public class DuplicationStatistics
{
    private long _partialHashes;
    private long _fullHashes;
    private long _totalFileBytes;
    private long _bytesProcessed;
    private long _bytesNotProcessed;
    private long _failedToHash;
    private long _filesToCheckForDuplicatesCount;
    private long _listOfDuplicatesProcessed;
    private long _allreadyDonePartials;
    private long _allreadyDoneFulls;
    private long _largestFileSize;
    private long _smallestFileSize = long.MaxValue;

    public long PartialHashes => Interlocked.Read(ref _partialHashes);
    public long FullHashes => Interlocked.Read(ref _fullHashes);
    public long TotalFileBytes => Interlocked.Read(ref _totalFileBytes);
    public long BytesProcessed => Interlocked.Read(ref _bytesProcessed);
    public long BytesNotProcessed => Interlocked.Read(ref _bytesNotProcessed);
    public long FailedToHash => Interlocked.Read(ref _failedToHash);

    public long FilesToCheckForDuplicatesCount
    {
        get => Interlocked.Read(ref _filesToCheckForDuplicatesCount);
        set => Interlocked.Exchange(ref _filesToCheckForDuplicatesCount, value);
    }

    public long ListOfDuplicatesProcessed => Interlocked.Read(ref _listOfDuplicatesProcessed);
    public long AllreadyDonePartials => Interlocked.Read(ref _allreadyDonePartials);
    public long AllreadyDoneFulls => Interlocked.Read(ref _allreadyDoneFulls);
    public long LargestFileSize => Interlocked.Read(ref _largestFileSize);
    public long SmallestFileSize => Interlocked.Read(ref _smallestFileSize);

    public void AddPartialHash() => Interlocked.Increment(ref _partialHashes);
    public void AddFullHash() => Interlocked.Increment(ref _fullHashes);
    public void AddFailedToHash() => Interlocked.Increment(ref _failedToHash);
    public void AddAllreadyDonePartial() => Interlocked.Increment(ref _allreadyDonePartials);
    public void AddAllreadyDoneFull() => Interlocked.Increment(ref _allreadyDoneFulls);
    public void AddListOfDuplicatesProcessed() => Interlocked.Increment(ref _listOfDuplicatesProcessed);

    public void AddBytesProcessed(long value) => Interlocked.Add(ref _bytesProcessed, value);
    public void AddTotalFileBytes(long value) => Interlocked.Add(ref _totalFileBytes, value);
    public void AddBytesNotProcessed(long value) => Interlocked.Add(ref _bytesNotProcessed, value);

    public void SeenFileSize(long value)
    {
        InterlockedMax(ref _largestFileSize, value);
        InterlockedMin(ref _smallestFileSize, value);
    }

    public long FilesProcessed => PartialHashes + FullHashes + AllreadyDonePartials + AllreadyDoneFulls + FailedToHash;

    private static void InterlockedMax(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var original = Interlocked.CompareExchange(ref target, value, current);
            if (original == current) return;
            current = original;
        }
    }

    private static void InterlockedMin(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value < current)
        {
            var original = Interlocked.CompareExchange(ref target, value, current);
            if (original == current) return;
            current = original;
        }
    }
}
