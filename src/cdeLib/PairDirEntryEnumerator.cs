﻿using System.Collections;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib;

public sealed class PairDirEntryEnumerator : IEnumerator<PairDirEntry>, IEnumerable<PairDirEntry>
{
    private readonly IEnumerable<RootEntry> _rootEntries;
    private PairDirEntry _current;
    private Stack<ICommonEntry> _entries;
    private ICommonEntry _parentDirEntry;
    private IEnumerator<ICommonEntry> _childEnumerator;

    public PairDirEntry Current => _current;

    object IEnumerator.Current => Current;

    public PairDirEntryEnumerator(RootEntry rootEntry)
    {
        _rootEntries = new List<RootEntry> { rootEntry };
        Reset();
    }

    public PairDirEntryEnumerator(IEnumerable<RootEntry> rootEntries)
    {
        _rootEntries = rootEntries;
        Reset();
    }

    private static Stack<ICommonEntry> StackOfRoots(IEnumerable<RootEntry> rootEntries)
    {
        var entries = new Stack<ICommonEntry>();
        foreach (var re in rootEntries)
        {
            if (re.Children != null && re.Children.Count > 0)
            {
                entries.Push(re);
            }
        }
        return entries;
    }

    public void Dispose()
    {
        _current = null;
        _entries = null;
        _childEnumerator?.Dispose();
    }

    public bool MoveNext()
    {
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
                _current = new PairDirEntry(_parentDirEntry, de);
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

    IEnumerator<PairDirEntry> IEnumerable<PairDirEntry>.GetEnumerator()
    {
        return new PairDirEntryEnumerator(_rootEntries);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new PairDirEntryEnumerator(_rootEntries);
    }
}