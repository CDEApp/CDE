using System.Collections.Concurrent;

namespace cdeLib.Infrastructure;

/// <summary>
/// Simple buffer pool for reducing memory allocations in high-throughput scenarios
/// </summary>
public class BufferPool
{
    private readonly ConcurrentQueue<byte[]> _buffers = new();
    private readonly int _bufferSize;
    private readonly int _maxBuffers;
    private volatile int _currentCount;

    public BufferPool(int bufferSize = 64 * 1024, int maxBuffers = 50)
    {
        _bufferSize = bufferSize;
        _maxBuffers = maxBuffers;
    }

    public byte[] Rent()
    {
        if (_buffers.TryDequeue(out var buffer))
        {
            return buffer;
        }
        return new byte[_bufferSize];
    }

    public void Return(byte[] buffer)
    {
        if (buffer == null || buffer.Length != _bufferSize) return;
        
        if (_currentCount < _maxBuffers)
        {
            _buffers.Enqueue(buffer);
            _currentCount++;
        }
    }

    public void Clear()
    {
        while (_buffers.TryDequeue(out _))
        {
            _currentCount--;
        }
    }
}