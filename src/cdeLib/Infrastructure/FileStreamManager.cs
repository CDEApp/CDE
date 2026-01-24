using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace cdeLib.Infrastructure;

/// <summary>
/// Manages pooled file streams with optimal buffer sizes for I/O operations
/// </summary>
public class FileStreamManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly ObjectPool<byte[]> _bufferPool;
    private volatile bool _disposed;

    public FileStreamManager()
    {
        _bufferPool = new ObjectPool<byte[]>(() => new byte[256 * 1024], null, 50);
    }

    /// <summary>
    /// Create an optimized FileStream for reading with async support
    /// </summary>
    public FileStream CreateReadStream(string filePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileStreamManager));
        
        return new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 256 * 1024,
            useAsync: true);
    }

    /// <summary>
    /// Create an optimized FileStream for writing with async support
    /// </summary>
    public FileStream CreateWriteStream(string filePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileStreamManager));
        
        return new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 256 * 1024,
            useAsync: true);
    }

    /// <summary>
    /// Get a reusable buffer from the pool
    /// </summary>
    public byte[] GetBuffer() => _bufferPool.Get();

    /// <summary>
    /// Return a buffer to the pool for reuse
    /// </summary>
    public void ReturnBuffer(byte[] buffer) => _bufferPool.Return(buffer);

    /// <summary>
    /// Get a per-file semaphore for controlling concurrent access
    /// </summary>
    public SemaphoreSlim GetFileLock(string filePath)
    {
        return _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Read file with optimized buffering and async I/O
    /// </summary>
    public async Task<byte[]> ReadAllBytesOptimizedAsync(string filePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileStreamManager));

        var fileLock = GetFileLock(filePath);
        await fileLock.WaitAsync();
        
        try
        {
            await using var stream = CreateReadStream(filePath);
            var buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer);
            return buffer;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// Write file with optimized buffering and async I/O
    /// </summary>
    public async Task WriteAllBytesOptimizedAsync(string filePath, byte[] data)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileStreamManager));

        var fileLock = GetFileLock(filePath);
        await fileLock.WaitAsync();
        
        try
        {
            await using var stream = CreateWriteStream(filePath);
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        // Dispose all semaphores
        foreach (var semaphore in _fileLocks.Values)
        {
            semaphore.Dispose();
        }
        _fileLocks.Clear();
        
        // Clear buffer pool
        _bufferPool.Dispose();
    }
}

/// <summary>
/// Static instance for application-wide file stream management
/// </summary>
public static class FileStreams
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<FileStreamManager> _instance = 
        new(() => new FileStreamManager(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static FileStreamManager Instance => _instance.Value;
}