using System.Collections;
using System.Collections.Generic;
using cdeLib.Entities;
using cdeLib.Infrastructure;

namespace cdeLib;

public sealed class PairDirEntryEnumerator : IEnumerator<IPairDirEntry>, IEnumerable<IPairDirEntry>
{
    private readonly IEnumerable<RootEntry> _rootEntries;
    private IPairDirEntry _current;
    private Stack<ICommonEntry> _entries;
    private ICommonEntry _parentDirEntry;
    private IEnumerator<ICommonEntry> _childEnumerator;
    private readonly bool _usePooling;

    public IPairDirEntry Current => _current;

    object IEnumerator.Current => Current;

    public PairDirEntryEnumerator(RootEntry rootEntry, bool usePooling = false)
    {
        _rootEntries = new List<RootEntry> { rootEntry };
        _usePooling = usePooling;
        Reset();
    }

    public PairDirEntryEnumerator(IEnumerable<RootEntry> rootEntries, bool usePooling = false)
    {
        _rootEntries = rootEntries;
        _usePooling = usePooling;
        Reset();
    }

    private static Stack<ICommonEntry> StackOfRoots(IEnumerable<RootEntry> rootEntries)
    {
        var entries = CollectionPool.GetCommonEntryStack();
        foreach (var re in rootEntries)
        {
            if (re.Children is { Count: > 0 })
            {
                entries.Push(re);
            }
        }

        return entries;
    }

    public void Dispose()
    {
        // Return pooled entry if we're using pooling
        if (_usePooling && _current is PooledPairDirEntry pooledEntry)
        {
            PairDirEntryPool.Return(pooledEntry);
        }

        _current = null;

        if (_entries != null)
        {
            CollectionPool.ReturnCommonEntryStack(_entries);
            _entries = null;
        }

        _childEnumerator?.Dispose();
    }

    public bool MoveNext()
    {
        // Return previous pooled entry if we're using pooling
        if (_usePooling && _current is PooledPairDirEntry previousPooledEntry)
        {
            PairDirEntryPool.Return(previousPooledEntry);
        }

        _current = null;
        if (_childEnumerator == null)
        {
            if (_entries.Count > 0)
            {
                var de = _entries.Pop();

                _parentDirEntry = de;
                _childEnumerator = de.Children.GetEnumerator();
            }
        }

        if (_childEnumerator != null)
        {
            if (_childEnumerator.MoveNext())
            {
                var de = _childEnumerator.Current;
                _current = _usePooling
                    ? PairDirEntryPool.Get(_parentDirEntry, de)
                    : new PairDirEntry(_parentDirEntry, de);
                if (de.IsDirectory && de.Children?.Count > 0)
                {
                    _entries.Push(de);
                }
            }
            else
            {
                _childEnumerator = null;
                MoveNext();
            }
        }

        return _current != null;
    }

    public void Reset()
    {
        _current = null;
        _entries = StackOfRoots(_rootEntries);
        _childEnumerator = null;
    }

    IEnumerator<IPairDirEntry> IEnumerable<IPairDirEntry>.GetEnumerator()
    {
        return new PairDirEntryEnumerator(_rootEntries, _usePooling);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new PairDirEntryEnumerator(_rootEntries, _usePooling);
    }
}