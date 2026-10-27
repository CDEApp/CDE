using System;
using System.Threading;
using cdeLib.Entities;

namespace cdeAppCore.Session;

/// <summary>
/// One loaded catalog: its source, the file it was loaded from, and the reader/writer lock that lets a
/// rescan swap the source while searches and listings hold read locks. The slot (and so the catalog id)
/// outlives any number of source swaps.
/// </summary>
internal sealed class CatalogSlot(IEntrySource source, string sourcePath) : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private int _rescanning;

    /// <summary>Replaced only under the write lock.</summary>
    public IEntrySource Source { get; private set; } = source;

    /// <summary>Exact file this catalog was loaded from; null if unknown (then it cannot be rescanned).</summary>
    public string SourcePath { get; private set; } = sourcePath;

    public bool IsRescanning => Volatile.Read(ref _rescanning) != 0;

    public bool TryBeginRescan() => Interlocked.CompareExchange(ref _rescanning, 1, 0) == 0;

    public void EndRescan() => Volatile.Write(ref _rescanning, 0);

    public CatalogReadLease EnterRead()
    {
        _lock.EnterReadLock(); // NOSONAR S2222/S7133: read lock is handed to the CatalogReadLease, which releases it in Dispose
        var current = Source;
        if (current == null)
        {
            _lock.ExitReadLock();
            throw new InvalidOperationException("The catalog is unavailable.");
        }

        return new CatalogReadLease(this, current);
    }

    /// <summary>Like <see cref="EnterRead"/> but returns false (holding nothing) when the slot has no source.</summary>
    public bool TryEnterRead(out CatalogReadLease lease)
    {
        _lock.EnterReadLock(); // NOSONAR S2222/S7133: read lock is handed to the CatalogReadLease, which releases it in Dispose
        var current = Source;
        if (current == null)
        {
            _lock.ExitReadLock();
            lease = default;
            return false;
        }

        lease = new CatalogReadLease(this, current);
        return true;
    }

    public void ExitRead() => _lock.ExitReadLock();

    /// <summary>Take the write lock, waiting for running readers. Polls so a cancel is honoured while waiting.</summary>
    public void EnterWrite(CancellationToken ct)
    {
        while (!_lock.TryEnterWriteLock(TimeSpan.FromMilliseconds(50))) // NOSONAR S7133: EnterWrite/ExitWrite are paired by the caller (CatalogSession.Rescan swap)
        {
            ct.ThrowIfCancellationRequested();
        }
    }

    public void ExitWrite() => _lock.ExitWriteLock();

    /// <summary>Install a new source. Caller holds the write lock.</summary>
    public void Replace(IEntrySource newSource, string newSourcePath)
    {
        Source = newSource;
        SourcePath = newSourcePath;
    }

    public void Dispose()
    {
        if (Source is IDisposable d) d.Dispose();
        Source = null;
        _lock.Dispose();
    }
}
