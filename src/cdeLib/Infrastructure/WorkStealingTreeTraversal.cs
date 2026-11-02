using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using Serilog;

namespace cdeLib.Infrastructure;

/// <summary>
/// High-performance parallel tree traversal with work stealing for better load balancing
/// </summary>
public class WorkStealingTreeTraversal
{
    private readonly WorkStealingQueue _workQueue;
    private readonly int _maxConcurrency;
    private readonly CancellationToken _cancellationToken;
    private Serilog.ILogger logger;
    private volatile int _totalEntriesProcessed;
    private volatile Exception _fault;

    public WorkStealingTreeTraversal(int maxConcurrency = 0, CancellationToken cancellationToken = default)
    {
        logger = Log.Logger;
        _maxConcurrency = maxConcurrency <= 0 ? Environment.ProcessorCount : maxConcurrency;
        _workQueue = new WorkStealingQueue(_maxConcurrency);
        _cancellationToken = cancellationToken;
        logger.Debug("WorkStealingTreeTraversal initialized with MaxConcurrency: {MaxConcurrency}", _maxConcurrency);

    }

    /// <summary>
    /// Traverse multiple root entries in parallel with work stealing
    /// </summary>
    public async Task TraverseAsync(IEnumerable<RootEntry> rootEntries,
        Func<ICommonEntry, ICommonEntry, Task<bool>> asyncProcessor)
    {
        // Get count efficiently without materializing if possible
        int rootCount = rootEntries.TryGetNonEnumeratedCount(out var count)
            ? count
            : rootEntries.Count();
        logger.Debug("Starting traversal with {RootCount} root entries", rootCount);

        // Initialize the work queue with root entries - enumerate directly without ToList
        var initialWork = rootEntries.Select(root => new TraversalWorkItem(null, root));
        _workQueue.AddWork(initialWork);

        logger.Debug("Added {InitialWorkCount} initial work items to queue", rootCount);

        // Workers run on the linked token: the caller's token, the timeout, or a failing worker cancels the rest.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30 minute timeout
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token);
        _fault = null;

        // Create worker tasks
        var workers = new Task[_maxConcurrency];
        for (int i = 0; i < _maxConcurrency; i++)
        {
            workers[i] = CreateWorkerTask(asyncProcessor, combinedCts);
        }

        logger.Debug("Created {WorkerCount} worker tasks", _maxConcurrency);

        try
        {
            await Task.WhenAll(workers);
            if (timeoutCts.IsCancellationRequested)
            {
                throw new OperationCanceledException(timeoutCts.Token); // workers stop quietly on the timeout; do not report partial results
            }
            logger.Debug("All workers completed successfully. Traversal finished. Total entries processed: {TotalEntriesProcessed}", _totalEntriesProcessed);
        }
        catch (Exception) when (_fault != null)
        {
            // The first processor failure is the cause; the other workers only saw the cancellation it triggered.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_fault).Throw();
            throw; // unreachable
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            logger.Warning("Work stealing traversal timed out after 30 minutes");
            throw new TimeoutException("Work stealing traversal timed out");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            logger.Debug("Traversal cancelled by user request");
            throw;
        }
    }

    private async Task CreateWorkerTask(Func<ICommonEntry, ICommonEntry, Task<bool>> processor,
        CancellationTokenSource workersCts)
    {
        var token = workersCts.Token;
        _workQueue.RegisterWorker();
        var idleCount = 0;
        const int maxIdleIterations = 100; // Maximum idle iterations before giving up

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_workQueue.TryGetWork(out var workItem))
                {
                    idleCount = 0; // Reset idle counter when work is found
                    await ProcessWorkItem(workItem, processor, token);
                }
                else
                {
                    // No work available - implement proper termination logic
                    idleCount++;

                    if (_workQueue.HasWork)
                    {
                        // There's still work in the system, but we couldn't get any
                        // Wait briefly and try again
                        await Task.Delay(1, token);
                        idleCount = Math.Max(0, idleCount - 1); // Reduce idle count since there's still work
                    }
                    else if (_workQueue.ActiveWorkers == 1)
                    {
                        // We're the last worker and there's no work - double check and wait a bit
                        // Increase wait time to ensure all async work completion is detected
                        await Task.Delay(100, token); // Give more time for any pending work to be added
                        if (!_workQueue.HasWork && _workQueue.ActiveWorkers == 1)
                        {
                            logger.Debug("Last worker ({ActiveWorkers}) found no work after final check, terminating", _workQueue.ActiveWorkers);
                            break;
                        }
                        logger.Debug("Last worker found work or more workers became active, continuing");
                        // If work appeared or more workers became active, continue
                    }
                    else if (idleCount >= maxIdleIterations)
                    {
                        // We've been idle too long - check if all workers are idle
                        if (!_workQueue.HasWork && _workQueue.ActiveWorkers > 0)
                        {
                            logger.Debug("Worker idle for {IdleCount} iterations with no work, terminating", idleCount);
                            break;
                        }
                        idleCount = 0; // Reset if there's still potential work
                    }
                    else
                    {
                        // Wait and try again
                        var delayMs = Math.Min(10, idleCount / 10 + 1); // Adaptive delay
                        await Task.Delay(delayMs, token);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep the first failure, then stop the other workers so it surfaces promptly.
            Interlocked.CompareExchange(ref _fault, ex, null);
            await workersCts.CancelAsync();
            throw;
        }
        finally
        {
            // Always unregistered, even when faulted, so idle/active termination detection never waits on a dead worker.
            _workQueue.UnregisterWorker();
            logger.Debug("Worker unregistered. Remaining active workers: {ActiveWorkers}", _workQueue.ActiveWorkers);
        }
    }

    private async Task ProcessWorkItem(TraversalWorkItem workItem,
        Func<ICommonEntry, ICommonEntry, Task<bool>> processor, CancellationToken token)
    {
        // A processor failure is not swallowed: it faults this worker and TraverseAsync rethrows it, so a find
        // never reports partial results as complete.
        // Match TraverseTreePair exactly: process this directory's children
        // Empty directories may not have Children initialized - skip them like TraverseTreePair does
        if (workItem.Entry.Children == null)
        {
            return;
        }

        // Process all children of this directory (matching TraverseTreePair behavior)
        foreach (var dirEntry in workItem.Entry.Children)
        {
            if (token.IsCancellationRequested)
            {
                return; // another worker failed (or the caller cancelled)
            }

            // Call processor for this child (parent=workItem.Entry, child=dirEntry)
            var shouldContinue = await processor(workItem.Entry, dirEntry);
            Interlocked.Increment(ref _totalEntriesProcessed);

            if (!shouldContinue)
            {
                break; // Stop processing if processor returns false (matches TraverseTreePair)
            }

            // If this child is a directory, add it to work queue for processing its children
            // This matches the stack.Push(dirEntry) in TraverseTreePair
            // The directory becomes the new "root" to process (parent=null), not a parent-child pair
            if (dirEntry.IsDirectory)
            {
                var childWorkItem = new TraversalWorkItem(null, dirEntry, workItem.Depth + 1);
                _workQueue.AddWork(childWorkItem);

                logger.Verbose("Added directory to work queue: {DirectoryPath} (Depth: {Depth})",
                    dirEntry.Path, workItem.Depth + 1);
            }
        }
    }
}