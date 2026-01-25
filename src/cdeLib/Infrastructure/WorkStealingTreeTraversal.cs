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
        var iEnumerable = rootEntries.ToList();
        var rootCount = iEnumerable.Count;
        logger.Debug("Starting traversal with {RootCount} root entries", rootCount);

        // Initialize the work queue with root entries
        var initialWork = iEnumerable.Select(root => new TraversalWorkItem(null, root));
        _workQueue.AddWork(initialWork);

        logger.Debug("Added {InitialWorkCount} initial work items to queue", rootCount);

        // Create worker tasks
        var workers = new Task[_maxConcurrency];
        for (int i = 0; i < _maxConcurrency; i++)
        {
            workers[i] = CreateWorkerTask(asyncProcessor);
        }

        logger.Debug("Created {WorkerCount} worker tasks", _maxConcurrency);

        // Wait for all workers to complete with timeout to prevent hanging
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30 minute timeout
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token);
        
        try
        {
            await Task.WhenAll(workers);
            logger.Debug("All workers completed successfully. Traversal finished. Total entries processed: {TotalEntriesProcessed}", _totalEntriesProcessed);
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

    private async Task CreateWorkerTask(Func<ICommonEntry, ICommonEntry, Task<bool>> processor)
    {
        _workQueue.RegisterWorker();
        var idleCount = 0;
        const int maxIdleIterations = 100; // Maximum idle iterations before giving up
        
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_workQueue.TryGetWork(out var workItem))
                {
                    idleCount = 0; // Reset idle counter when work is found
                    await ProcessWorkItem(workItem, processor);
                }
                else
                {
                    // No work available - implement proper termination logic
                    idleCount++;
                    
                    if (_workQueue.HasWork)
                    {
                        // There's still work in the system, but we couldn't get any
                        // Wait briefly and try again
                        await Task.Delay(1, _cancellationToken);
                        idleCount = Math.Max(0, idleCount - 1); // Reduce idle count since there's still work
                    }
                    else if (_workQueue.ActiveWorkers == 1)
                    {
                        // We're the last worker and there's no work - double check and wait a bit
                        // Increase wait time to ensure all async work completion is detected
                        await Task.Delay(100, _cancellationToken); // Give more time for any pending work to be added
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
                        await Task.Delay(delayMs, _cancellationToken);
                    }
                }
            }
        }
        finally
        {
            _workQueue.UnregisterWorker();
            logger.Debug("Worker unregistered. Remaining active workers: {ActiveWorkers}", _workQueue.ActiveWorkers);
        }
    }

    private async Task ProcessWorkItem(TraversalWorkItem workItem, 
        Func<ICommonEntry, ICommonEntry, Task<bool>> processor)
    {
        try
        {
            // Match TraverseTreePair exactly: process this directory's children
            // Empty directories may not have Children initialized - skip them like TraverseTreePair does
            if (workItem.Entry.Children == null)
            {
                return;
            }

            // Process all children of this directory (matching TraverseTreePair behavior)
            foreach (var dirEntry in workItem.Entry.Children)
            {
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
        catch (Exception ex)
        {
            logger.Warning(ex, "Error processing work item for entry: {EntryPath}", workItem.Entry?.Path ?? "null");
        }
    }
}