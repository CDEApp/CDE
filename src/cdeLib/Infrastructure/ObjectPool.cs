using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace cdeLib.Infrastructure;

/// <summary>
/// Generic object pool for reducing allocations of frequently created objects
/// </summary>
public interface IObjectPool<T> where T : class
{
    T Get();
    void Return(T item);
    void Clear();
}

/// <summary>
/// High-performance object pool implementation with configurable creation and reset policies
/// </summary>
public class ObjectPool<T> : IObjectPool<T>, IDisposable where T : class
{
    private readonly ConcurrentQueue<T> _objects = new();
    private readonly Func<T> _objectGenerator;
    private readonly Action<T> _resetAction;
    private readonly int _maxObjects;
    private volatile int _currentCount;
    private volatile bool _disposed;

    public ObjectPool(Func<T> objectGenerator, Action<T> resetAction = null, int maxObjects = 100)
    {
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _resetAction = resetAction;
        _maxObjects = maxObjects;
    }

    public T Get()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ObjectPool<T>));
        
        if (_objects.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _currentCount);
            return item;
        }

        return _objectGenerator();
    }

    public void Return(T item)
    {
        if (_disposed || item == null) return;

        if (_currentCount < _maxObjects)
        {
            _resetAction?.Invoke(item);
            _objects.Enqueue(item);
            Interlocked.Increment(ref _currentCount);
        }
    }

    public void Clear()
    {
        while (_objects.TryDequeue(out var item))
        {
            if (item is IDisposable disposable)
                disposable.Dispose();
        }
        _currentCount = 0;
    }

    public int Count => _currentCount;

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        Clear();
    }
}

/// <summary>
/// Static object pool manager for commonly used objects
/// </summary>
public static class PoolManager
{
    private static readonly ObjectPool<StringBuilder> StringBuilderPool = 
        new(() => new StringBuilder(256), sb => sb.Clear(), 50);
    
    private static readonly ObjectPool<List<string>> StringListPool = 
        new(() => new List<string>(), list => list.Clear(), 30);

    public static StringBuilder GetStringBuilder() => StringBuilderPool.Get();
    public static void ReturnStringBuilder(StringBuilder sb) => StringBuilderPool.Return(sb);
    
    public static List<string> GetStringList() => StringListPool.Get();
    public static void ReturnStringList(List<string> list) => StringListPool.Return(list);
}