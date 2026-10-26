using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using RootEntry = cdeLib.Entities.RootEntry;
using TraverseFunc = cdeLib.Entities.TraverseFunc;

namespace cdeLib;

public class FindOptions
{
    public string Pattern { get; set; }

    public bool RegexMode { get; set; }

    public bool IncludePath { get; set; }

    public bool IncludeFiles { get; set; }

    public bool IncludeFolders { get; set; }

    public int LimitResultCount { get; set; } // consider making this a multiple of ProgressModifier

    public int ProgressModifier { get; set; }

    public bool FromSizeEnable { get; set; }

    public long FromSize { get; set; }

    public bool ToSizeEnable { get; set; }

    public long ToSize { get; set; }

    public bool FromDateEnable { get; set; }

    public DateTime FromDate { get; set; }

    public bool ToDateEnable { get; set; }

    public DateTime ToDate { get; set; }

    public bool FromHourEnable { get; set; }

    public TimeSpan FromHour { get; set; }

    public bool ToHourEnable { get; set; }

    public TimeSpan ToHour { get; set; }

    public bool NotOlderThanEnable { get; set; }

    public DateTime NotOlderThan { get; set; }

    public int ProgressEnd { get; set; }

    private int _threadSafeProgressCount;
    private volatile int _lastReportedProgress;
    private long _lastProgressTimestamp;
    private long _searchStartTimestamp;

    private readonly int[] _dummyProgressCount = new int[1];

    // Run cancellation + progress housekeeping every 4096 entries (mask = 4096-1). Frequent enough to
    // feel instant even on a slow regex, cheap enough to be negligible on a fast full scan.
    private const int CancelCheckMask = 4096 - 1;

    // Stream progress/results at most every ~100ms (time-based, like the old async path), so a long
    // search updates the UI smoothly rather than in large infrequent entry-count-based chunks.
    // Stored as a tick threshold so the hot-path check is a plain subtraction (no multiply/overflow).
    private static readonly long ProgressIntervalTicks = Stopwatch.Frequency / 10;

    public int SkipCount { get; set; }

    public int ProgressCount => _threadSafeProgressCount;

    /// <summary>
    /// Called for every entry that matches the predicate entry.
    /// </summary>
    public TraverseFunc VisitorFunc { get; set; }

    /// <summary>
    /// Called for reporting progress to caller (low-level non-UI callback: visited count, total).
    /// </summary>
    public Action<int, int> ProgressFunc { get; set; }

    /// <summary>
    /// Structured progress channel. Reported alongside <see cref="ProgressFunc"/> on the same throttle.
    /// </summary>
    public IProgress<SearchProgress> Progress { get; set; }

    /// <summary>
    /// Cooperative cancellation for both the synchronous <see cref="Find"/> and asynchronous
    /// <see cref="FindAsync"/> paths. Replaces the previous WinForms <c>BackgroundWorker</c> coupling.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public Func<ICommonEntry, ICommonEntry, bool> PatternMatcher { get; set; }

    public FindOptions()
    {
        LimitResultCount = 10000;
        ProgressModifier = int.MaxValue; // huge m0n.
        IncludeFiles = true;
        IncludeFolders = true;
    }

    public void Find(IEnumerable<RootEntry> rootEntries)
    {
        if (VisitorFunc == null)
        {
            return;
        }

        _searchStartTimestamp = Stopwatch.GetTimestamp();
        int[] limitCount = [LimitResultCount];
        if (ProgressFunc == null || ProgressModifier == 0)
        {
            // dummy func and huge progressModifier so won't call progressFunc anyway.
            ProgressFunc = delegate { };
            ProgressModifier = int.MaxValue;
        }

        // ReSharper disable PossibleMultipleEnumeration
        ProgressEnd = rootEntries.TotalFileEntries();
        // ReSharper restore PossibleMultipleEnumeration
        ProgressFunc(_threadSafeProgressCount, ProgressEnd); // Start of process Progress report.
        PatternMatcher = GetPatternMatcher();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
            CancellationToken = CancellationToken.None
        };

        // Pre-sort for better initial distribution - use Array.Sort for efficiency
        var sortedRootEntries = rootEntries as RootEntry[] ?? rootEntries.ToArray();
        Array.Sort(sortedRootEntries, (a, b) => b.DirEntryCount.CompareTo(a.DirEntryCount));

        var findFunc = GetFindFunc(_dummyProgressCount, limitCount);
        // ReSharper disable PossibleMultipleEnumeration

        Parallel.ForEach(sortedRootEntries, parallelOptions, rootEntry =>
        {
            // Use single-entry overload to avoid array allocation
            EntryHelper.TraverseTreePair(rootEntry, findFunc);
        });
        ProgressFunc(ProgressEnd, ProgressEnd); // end of Progress - always report 100%
    }

    /// <summary>
    /// High-performance async find with work stealing for better load balancing
    /// </summary>
    public async Task FindAsync(IEnumerable<RootEntry> rootEntries, CancellationToken cancellationToken = default)
    {
        if (VisitorFunc == null)
        {
            return;
        }

        // Honour both the explicit token and any token already set on the options.
        if (cancellationToken.CanBeCanceled)
        {
            CancellationToken = cancellationToken;
        }
        _searchStartTimestamp = Stopwatch.GetTimestamp();

        // Setup progress tracking
        int[] limitCount = [LimitResultCount];
        if (ProgressFunc == null || ProgressModifier == 0)
        {
            ProgressFunc = delegate { };
            ProgressModifier = int.MaxValue;
        }

        ProgressEnd = rootEntries.TotalFileEntries();
        ProgressFunc(_threadSafeProgressCount, ProgressEnd);
        PatternMatcher = GetPatternMatcher();

        var watch = Stopwatch.StartNew();

        // Create work-stealing traversal with optimal concurrency
        var maxConcurrency = Math.Min(Environment.ProcessorCount, 8); // Cap at 8 for I/O bound operations

        var traversal = new Infrastructure.WorkStealingTreeTraversal(maxConcurrency, CancellationToken);

        // Create an async processor function
        var asyncProcessor = CreateAsyncProcessor(limitCount);

        try
        {
            // Execute parallel traversal with work stealing
            await traversal.TraverseAsync(rootEntries, asyncProcessor);
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            // Handle cancellation gracefully
        }

        ProgressFunc(ProgressEnd, ProgressEnd); // Always report 100%
        watch.Stop();
        Debug.WriteLine($"Async Execution Time: {watch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Create an async processor function for work stealing traversal
    /// </summary>
    private Func<ICommonEntry, ICommonEntry, Task<bool>> CreateAsyncProcessor(int[] limitCount)
    {
        // Cache predicate once outside the lambda to avoid per-entry delegate allocation
        var findPredicate = GetFindPredicate();

        return async (parent, dirEntry) =>
        {
            // Handle null parent (root entries)
            var p = parent ?? dirEntry;

            var currentCount = Interlocked.Increment(ref _threadSafeProgressCount);

            if (currentCount <= SkipCount)
            {
                return true; // Skip enforced
            }

            // Honour cancellation promptly, independently of throttled progress reporting (see GetFindFunc).
            if ((currentCount & CancelCheckMask) == 0 && CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            // Rate-limited progress reporting with non-blocking UI update
            if (ProgressModifier > 0 && ShouldReportProgress(currentCount))
            {
                var now = Stopwatch.GetTimestamp();
                var last = Interlocked.Read(ref _lastProgressTimestamp);
                var elapsedMs = (now - last) * 1000 / Stopwatch.Frequency;

                if (elapsedMs >= 100) // 100ms minimum between reports
                {
                    if (Interlocked.CompareExchange(ref _lastProgressTimestamp, now, last) == last)
                    {
                        // Use ThreadPool to avoid blocking worker thread on UI marshaling
                        var count = currentCount;
                        var end = ProgressEnd;
                        var elapsed = Stopwatch.GetElapsedTime(_searchStartTimestamp);
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            ProgressFunc(count, end);
                            Progress?.Report(new SearchProgress(count, end, elapsed));
                        });
                    }
                }

                // Check for cancellation with minimal overhead
                if (CancellationToken.IsCancellationRequested)
                {
                    return false;
                }
            }

            // Apply cached find predicate
            if (findPredicate(p, dirEntry))
            {
                // Execute visitor function
                var shouldContinue = VisitorFunc(p, dirEntry);
                if (!shouldContinue || Interlocked.Decrement(ref limitCount[0]) <= 0)
                {
                    return false;
                }
            }

            // Optimized cooperative cancellation with adaptive yielding
            if (currentCount % 2000 == 0) // Reduced frequency for better performance
            {
                // Use ConfigureAwait(false) to avoid unnecessary context switching
                await Task.Delay(0).ConfigureAwait(false);
            }
            else if (currentCount % 500 == 0)
            {
                // Lighter yield for more frequent cooperation
                await Task.Yield();
            }

            return true;
        };
    }

    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();
    private const int MaxRegexCacheSize = 100; // Prevent unbounded growth

    private Func<ICommonEntry, ICommonEntry, bool> GetPatternMatcher()
    {
        // Cache properties locally to avoid repeated field access
        var pattern = Pattern;
        var includePath = IncludePath;

        // Fast path for empty pattern
        if (string.IsNullOrEmpty(pattern))
            return (p, d) => true;

        if (RegexMode)
        {
            // Evict oldest entries if cache is too large (simple size-based eviction)
            if (RegexCache.Count > MaxRegexCacheSize)
            {
                // Remove ~20% of entries to avoid frequent evictions
                var toRemove = RegexCache.Keys.Take(MaxRegexCacheSize / 5).ToArray();
                foreach (var key in toRemove)
                {
                    RegexCache.TryRemove(key, out _);
                }
            }

            var regex = RegexCache.GetOrAdd(pattern, p =>
                new Regex(p, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase));

            return includePath
                ? (p, d) => regex.IsMatch(EntryHelper.MakeFullPathPooled(p, d))
                : (p, d) => regex.IsMatch(d.Path);
        }

        // String matching with StringComparison for better performance.
        // Path mode uses the allocation-free span matcher (avoids building a full-path string per
        // candidate — the dominant allocator in path queries; see search baseline).
        return includePath
            ? (p, d) => EntryHelper.FullPathContains(p, d, pattern, StringComparison.OrdinalIgnoreCase)
            : (p, d) => d.Path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private TraverseFunc GetFindFunc(int[] progressCount, int[] limitCount)
    {
        var findPredicate = GetFindPredicate();
        return FindFunc;

        bool FindFunc(ICommonEntry p, ICommonEntry dirEntry)
        {
            var currentCount = Interlocked.Increment(ref _threadSafeProgressCount);
            progressCount[0] = currentCount;

            if (currentCount <= SkipCount)
            {
                // skip enforced
                return true;
            }

            // Periodic housekeeping behind a cheap entry-count gate (~every 4096 entries):
            //   1. Honour cancellation promptly (was tied to the ~50k-entry progress throttle, which
            //      made a slow search ignore Cancel for tens of thousands of entries).
            //   2. Stream progress/results on a ~100ms timer so results appear smoothly during a long
            //      search instead of in large infrequent chunks. This matches the responsiveness of
            //      the old async path (which felt faster purely because it streamed every 100ms),
            //      while keeping the synchronous path's much higher raw throughput.
            if ((currentCount & CancelCheckMask) == 0)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return false; // end the find.
                }

                if (ProgressFunc != null && ProgressModifier > 0)
                {
                    var now = Stopwatch.GetTimestamp();
                    var last = Interlocked.Read(ref _lastProgressTimestamp);
                    if (now - last >= ProgressIntervalTicks
                        && Interlocked.CompareExchange(ref _lastProgressTimestamp, now, last) == last)
                    {
                        ProgressFunc(currentCount, ProgressEnd);
                        Progress?.Report(new SearchProgress(currentCount, ProgressEnd,
                            Stopwatch.GetElapsedTime(_searchStartTimestamp)));
                    }
                }
            }

            if (findPredicate(p, dirEntry))
            {
                if (!VisitorFunc(p, dirEntry) || --limitCount[0] <= 0)
                {
                    return false; // end the find.
                }
            }

            return true;
        }
    }

    public void ResetProgress()
    {
        _threadSafeProgressCount = 0;
        _lastReportedProgress = 0;
        _lastProgressTimestamp = 0;
    }

    private bool ShouldReportProgress(int currentCount)
    {
        // Adaptive progress reporting based on total entry count to reduce contention
        // For large datasets, report less frequently to minimize overhead
        var baseModifier = ProgressEnd switch
        {
            > 10_000_000 => 100_000,  // 10M+ entries: report every 100K
            > 1_000_000 => 50_000,    // 1M+ entries: report every 50K
            > 100_000 => 10_000,      // 100K+ entries: report every 10K
            _ => 1_000                // Small datasets: report every 1K
        };

        var adaptiveModifier = Math.Max(ProgressModifier / 2, baseModifier);

        // Report progress every adaptiveModifier entries, but use lock-free comparison
        if (currentCount % adaptiveModifier != 0)
            return false;

        // Only report if we haven't reported this value recently (reduces duplicate reports in parallel)
        var lastReported = _lastReportedProgress;
        var minimumDelta = adaptiveModifier / 2;

        if (currentCount <= lastReported + minimumDelta)
            return false;

        // Try to update last reported (lock-free with retry limit for better async performance)
        var originalLastReported = lastReported;
        var result = Interlocked.CompareExchange(ref _lastReportedProgress, currentCount, lastReported) ==
                     originalLastReported;

        // If CAS failed, check if someone else already reported a more recent progress
        if (!result)
        {
            var currentLastReported = _lastReportedProgress;
            return currentCount > currentLastReported + minimumDelta;
        }

        return true;
    }

    /// <summary>
    /// Builds an optimized predicate that only evaluates enabled filters.
    /// This reduces branch mispredictions and avoids checking disabled filters.
    /// </summary>
    private TraverseFunc GetFindPredicate()
    {
        // Start with type filter (always needed)
        Func<ICommonEntry, ICommonEntry, bool> predicate;

        if (IncludeFiles && IncludeFolders)
            predicate = static (p, d) => true;
        else if (IncludeFiles)
            predicate = static (p, d) => !d.IsDirectory;
        else if (IncludeFolders)
            predicate = static (p, d) => d.IsDirectory;
        else
            return static (p, d) => false; // Nothing to find

        // Chain only enabled size filters
        if (FromSizeEnable)
        {
            var prev = predicate;
            var fromSize = FromSize;
            predicate = (p, d) => prev(p, d) && d.Size >= fromSize;
        }

        if (ToSizeEnable)
        {
            var prev = predicate;
            var toSize = ToSize;
            predicate = (p, d) => prev(p, d) && d.Size <= toSize;
        }

        // Chain only enabled date filters
        if (FromDateEnable)
        {
            var prev = predicate;
            var fromDate = FromDate;
            predicate = (p, d) => prev(p, d) && !d.IsModifiedBad && d.Modified >= fromDate;
        }

        if (ToDateEnable)
        {
            var prev = predicate;
            var toDate = ToDate;
            predicate = (p, d) => prev(p, d) && !d.IsModifiedBad && d.Modified <= toDate;
        }

        // Chain only enabled hour filters
        if (FromHourEnable)
        {
            var prev = predicate;
            var fromHourSeconds = FromHour.TotalSeconds;
            predicate = (p, d) => prev(p, d) && !d.IsModifiedBad && fromHourSeconds <= d.Modified.TimeOfDay.TotalSeconds;
        }

        if (ToHourEnable)
        {
            var prev = predicate;
            var toHourSeconds = ToHour.TotalSeconds;
            predicate = (p, d) => prev(p, d) && !d.IsModifiedBad && toHourSeconds >= d.Modified.TimeOfDay.TotalSeconds;
        }

        if (NotOlderThanEnable)
        {
            var prev = predicate;
            var notOlderThan = NotOlderThan;
            predicate = (p, d) => prev(p, d) && !d.IsModifiedBad && d.Modified >= notOlderThan;
        }

        // Pattern matcher last (most expensive)
        var matcher = PatternMatcher;
        var final = predicate;
        return (p, d) => final(p, d) && matcher(p, d);
    }
}