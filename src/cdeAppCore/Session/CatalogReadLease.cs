using System;
using cdeLib.Entities;

namespace cdeAppCore.Session;

/// <summary>
/// A read lock on one catalog slot plus the source that was current when it was taken. While held, a
/// rescan swap of that catalog waits. Must be disposed on the thread that acquired it.
/// </summary>
public readonly struct CatalogReadLease : IDisposable
{
    private readonly CatalogSlot _slot;

    internal CatalogReadLease(CatalogSlot slot, IEntrySource source)
    {
        _slot = slot;
        Source = source;
    }

    public IEntrySource Source { get; }

    /// <summary>The file this catalog was loaded from; null if unknown. Stable while the lease is held (a rescan swap waits).</summary>
    public string SourcePath => _slot?.SourcePath;

    public void Dispose() => _slot?.ExitRead();
}
