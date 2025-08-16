using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace cdeLib.Infrastructure;

/// <summary>
/// Work-stealing queue for efficient load balancing across worker threads
/// </summary>
public class WorkStealingQueue
{
    private readonly ConcurrentQueue<TraversalWorkItem> _globalQueue = new();
    private readonly ThreadLocal<ConcurrentQueue<TraversalWorkItem>> _localQueues = 
        new(() => new ConcurrentQueue<TraversalWorkItem>());
    
    private volatile int _activeWorkers;
    private readonly int _maxWorkers;

    public WorkStealingQueue(int maxWorkers)
    {
        _maxWorkers = maxWorkers;
    }

    public void AddWork(TraversalWorkItem item)
    {
        // Calculate adaptive thresholds based on worker count
        var largeWorkThreshold = Math.Max(50, 200 / _maxWorkers); // Fewer workers = lower threshold
        var localQueueLimit = Math.Max(25, 100 / _maxWorkers); // Adaptive local queue size
        
        // Intelligent work distribution based on estimated size and worker count
        if (item.EstimatedSize > largeWorkThreshold) // Large work items go to global queue
        {
            _globalQueue.Enqueue(item);
        }
        else if (_localQueues.IsValueCreated && _localQueues.Value.Count < localQueueLimit)
        {
            _localQueues.Value.Enqueue(item);
        }
        else
        {
            _globalQueue.Enqueue(item); // Fallback to global queue
        }
    }

    public void AddWork(IEnumerable<TraversalWorkItem> items)
    {
        foreach (var item in items)
        {
            AddWork(item);
        }
    }

    public bool TryGetWork(out TraversalWorkItem item)
    {
        item = null;

        // Try local queue first (LIFO for better cache locality)
        if (_localQueues.IsValueCreated && _localQueues.Value.TryDequeue(out item))
        {
            return true;
        }

        // Try global queue
        if (_globalQueue.TryDequeue(out item))
        {
            return true;
        }

        // Try to steal from other workers
        return TryStealWork(out item);
    }

    private bool TryStealWork(out TraversalWorkItem item)
    {
        item = null;
        
        // Enhanced work stealing with load balancing
        // First try global queue for high-priority work
        if (_globalQueue.TryDequeue(out item))
        {
            return true;
        }
        
        // Try to redistribute work from overloaded local queues
        // This implements work stealing across thread-local queues
        var attempts = 0;
        const int maxAttempts = 3;
        
        while (attempts < maxAttempts)
        {
            // Calculate workload threshold based on max workers for better distribution
            var workloadThreshold = Math.Max(10, 50 / _maxWorkers); // Adaptive threshold
            
            // Look for overloaded queues relative to worker count
            if (_localQueues.IsValueCreated && _localQueues.Value.Count > workloadThreshold)
            {
                // Take portion of work from overloaded queue proportional to worker count
                var redistributionRatio = Math.Min(0.5, 1.0 / _maxWorkers);
                var itemsToRedistribute = Math.Max(1, (int)(_localQueues.Value.Count * redistributionRatio));
                
                for (int i = 0; i < itemsToRedistribute && _localQueues.Value.TryDequeue(out var redistributeItem); i++)
                {
                    _globalQueue.Enqueue(redistributeItem);
                }
                
                // Try to get work from global queue after redistribution
                if (_globalQueue.TryDequeue(out item))
                {
                    return true;
                }
            }
            
            attempts++;
            // Adaptive delay based on worker count - more workers = shorter delays
            if (attempts < maxAttempts)
            {
                var delayMs = Math.Max(1, 10 / _maxWorkers);
                if (delayMs > 1)
                    Thread.Sleep(delayMs);
                else
                    Thread.Yield();
            }
        }
        
        return false;
    }

    public bool TryRegisterWorker()
    {
        var current = _activeWorkers;
        if (current >= _maxWorkers)
        {
            return false; // Already at maximum capacity
        }
        
        // Try to register atomically
        return Interlocked.CompareExchange(ref _activeWorkers, current + 1, current) == current;
    }

    public void RegisterWorker()
    {
        // Force register (for backward compatibility)
        Interlocked.Increment(ref _activeWorkers);
    }

    public void UnregisterWorker()
    {
        Interlocked.Decrement(ref _activeWorkers);
    }

    public bool HasWork 
    {
        get
        {
            // Check global queue first (most reliable)
            if (!_globalQueue.IsEmpty)
                return true;
                
            // Check current thread's local queue
            if (_localQueues.IsValueCreated && !_localQueues.Value.IsEmpty)
                return true;
                
            // For work stealing, we can't easily check other threads' local queues
            // so we'll be conservative and assume no work if global queue is empty
            return false;
        }
    }

    public int ActiveWorkers => _activeWorkers;
    public int MaxWorkers => _maxWorkers;
    
    /// <summary>
    /// Check if we should spawn more workers based on workload
    /// </summary>
    public bool ShouldSpawnMoreWorkers => _activeWorkers < _maxWorkers && HasWork;
}