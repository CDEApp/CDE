using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

    private readonly int[] _dummyProgressCount = new int[1];

    public int SkipCount { get; set; }

    public int ProgressCount => _threadSafeProgressCount;

    /// <summary>
    /// Called for every entry that matches the predicate entry.
    /// </summary>
    public TraverseFunc VisitorFunc { get; set; }

    /// <summary>
    /// Called for reporting progress to caller.
    /// </summary>
    public Action<int, int> ProgressFunc { get; set; }

    public BackgroundWorker Worker { get; set; }

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

        int[] limitCount = { LimitResultCount };
        if (ProgressFunc == null || ProgressModifier == 0)
        {
            // dummy func and huge progressModifier so wont call progressFunc anyway.
            ProgressFunc = delegate { };
            ProgressModifier = int.MaxValue;
        }

        // ReSharper disable PossibleMultipleEnumeration
        ProgressEnd = rootEntries.TotalFileEntries();
        // ReSharper restore PossibleMultipleEnumeration
        ProgressFunc(_threadSafeProgressCount, ProgressEnd); // Start of process Progress report.
        PatternMatcher = GetPatternMatcher();

        var findFunc = GetFindFunc(_dummyProgressCount, limitCount);
        // ReSharper disable PossibleMultipleEnumeration

        var watch = Stopwatch.StartNew();
        Parallel.ForEach(rootEntries, (rootEntry) =>
        {
            //TODO: Parallel breaks the progress percentage, need to fix.
            EntryHelper.TraverseTreePair(new List<ICommonEntry> { rootEntry }, findFunc);
        });
        ProgressFunc(ProgressEnd, ProgressEnd); // end of Progress - always report 100%

    /// <summary>
    /// High-performance async find with work stealing for better load balancing
    /// </summary>
    public async Task FindAsync(IEnumerable<RootEntry> rootEntries, CancellationToken cancellationToken = default)
    {
        if (VisitorFunc == null)
        {
            return;
        }

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

        var traversal = new Infrastructure.WorkStealingTreeTraversal(maxConcurrency, cancellationToken);

        // Create async processor function
        var asyncProcessor = CreateAsyncProcessor(limitCount);

        try
        {
            // Execute parallel traversal with work stealing
            await traversal.TraverseAsync(rootEntries, asyncProcessor);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
        return async (parent, dirEntry) =>
        {
            // Handle null parent (root entries)
            var p = parent ?? dirEntry;

            var currentCount = Interlocked.Increment(ref _threadSafeProgressCount);

            if (currentCount <= SkipCount)
            {
                return true; // Skip enforced
            }

            // Optimized async progress reporting with batching
            if (ProgressModifier > 0 && ShouldReportProgress(currentCount))
            {
                // Use ConfigureAwait(false) for better performance in async context
                // Fire-and-forget progress reporting to avoid blocking worker threads
                _ = Task.Run(async () =>
                {
                    try
                    {
                        ProgressFunc(currentCount, ProgressEnd);
                        // Small delay to prevent overwhelming the UI thread
                        await Task.Delay(1).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore progress reporting errors to prevent work interruption
                    }
                });

                // Check for cancellation with minimal overhead
                if (Worker?.CancellationPending == true)
                {
                    return false;
                }
            }

            // Apply find predicate
            var findPredicate = GetFindPredicate();
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

    public Func<ICommonEntry, ICommonEntry, bool> GetPatternMatcher()
    {
        Func<ICommonEntry, ICommonEntry, bool> matcher;
        if (RegexMode)
        {
            var regex = RegexCache.GetOrAdd(Pattern, pattern =>
                new Regex(pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase));

            return IncludePath
                ? (p, d) => regex.IsMatch(p.MakeFullPath(d))
                : (p, d) => regex.IsMatch(d.Path);
        }

        // String matching with StringComparison for better performance
        return IncludePath
            ? (p, d) => p.MakeFullPath(d).Contains(Pattern, StringComparison.OrdinalIgnoreCase)
            : (p, d) => d.Path.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
    }

    public TraverseFunc GetFindFunc(int[] progressCount, int[] limitCount)
    {
        var findPredicate = GetFindPredicate();

        bool FindFunc(ICommonEntry p, ICommonEntry dirEntry)
        {
            var currentCount = Interlocked.Increment(ref _threadSafeProgressCount);
            progressCount[0] = currentCount;

            if (currentCount <= SkipCount)
            {
                // skip enforced
                return true;
            }

            // Use lock-free progress reporting with reduced frequency
            if (ProgressModifier > 0 && ShouldReportProgress(currentCount))
            {
                ProgressFunc(currentCount, ProgressEnd);
                // only check for cancel on progress reports.
                if (Worker?.CancellationPending == true)
                {
                    return false; // end the find.
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

        return FindFunc;
    }

    public void ResetProgress()
    {
        _threadSafeProgressCount = 0;
        _lastReportedProgress = 0;
    }

    private bool ShouldReportProgress(int currentCount)
    {
        // Report progress every ProgressModifier entries, but use lock-free comparison
        if (currentCount % ProgressModifier != 0)
            return false;

        // Only report if we haven't reported this value recently (reduces duplicate reports in parallel)
        var lastReported = _lastReportedProgress;
        if (currentCount <= lastReported + ProgressModifier / 2)
            return false;

        // Try to update last reported (lock-free)
        return Interlocked.CompareExchange(ref _lastReportedProgress, currentCount, lastReported) == lastReported;
    }

    public TraverseFunc GetFindPredicate()
    {
        return (p, d) =>
            (d.IsDirectory && IncludeFolders || !d.IsDirectory && IncludeFiles)
            && (!FromSizeEnable || FromSizeEnable && d.Size >= FromSize)
            && (!ToSizeEnable || ToSizeEnable && d.Size <= ToSize)
            && (!FromDateEnable || FromDateEnable && !d.IsModifiedBad && d.Modified >= FromDate)
            && (!ToDateEnable || ToDateEnable && !d.IsModifiedBad && d.Modified <= ToDate)
            && (!FromHourEnable || FromHourEnable && !d.IsModifiedBad
                                                  && FromHour.TotalSeconds <= d.Modified.TimeOfDay.TotalSeconds)
            && (!ToHourEnable || ToHourEnable && !d.IsModifiedBad
                                              && ToHour.TotalSeconds >= d.Modified.TimeOfDay.TotalSeconds)
            && (!NotOlderThanEnable || NotOlderThanEnable
                && !d.IsModifiedBad && d.Modified >= NotOlderThan)
            && PatternMatcher(p, d);
    }
}