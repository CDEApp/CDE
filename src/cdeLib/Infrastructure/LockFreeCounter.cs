using System.Threading;

namespace cdeLib.Infrastructure;

/// <summary>
/// Lock-free atomic counter optimized for high-frequency increments
/// with batched progress reporting to minimize overhead
/// </summary>
public class LockFreeCounter
{
    private volatile int _count;
    private volatile int _reportCounter;
    private readonly int _reportThreshold;

    public LockFreeCounter(int reportThreshold = 100)
    {
        _reportThreshold = reportThreshold;
    }

    public int Count => _count;

    /// <summary>
    /// Increment counter and return true if reporting threshold reached
    /// </summary>
    public bool IncrementAndShouldReport()
    {
        Interlocked.Increment(ref _count);
        return Interlocked.Increment(ref _reportCounter) >= _reportThreshold;
    }

    /// <summary>
    /// Reset report counter after reporting
    /// </summary>
    public void ResetReportCounter()
    {
        _reportCounter = 0;
    }

    /// <summary>
    /// Reset all counters
    /// </summary>
    public void Reset()
    {
        _count = 0;
        _reportCounter = 0;
    }

    /// <summary>
    /// Simple increment without reporting logic - fastest option
    /// </summary>
    public int SimpleIncrement()
    {
        return ++_count;
    }
}