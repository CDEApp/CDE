using System.Collections;
using System.Collections.Generic;
using cdeLib.Entities;
using cdeLib.Infrastructure;

namespace cdeLib;

public sealed class DirEntryEnumerator : IEnumerator<ICommonEntry>, IEnumerable<ICommonEntry>
{
    private readonly IEnumerable<RootEntry> _rootEntries;
    private ICommonEntry _current;
    private Stack<ICommonEntry> _entries;
    private IEnumerator<ICommonEntry> _childEnumerator;

    public ICommonEntry Current => _current;
    object IEnumerator.Current => Current;

    public DirEntryEnumerator(RootEntry rootEntry)
    {
        _rootEntries = new List<RootEntry> { rootEntry };
        Reset();
    }

    public DirEntryEnumerator(IEnumerable<RootEntry> rootEntries)
    {
        _rootEntries = rootEntries;
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
        _current = null;
        return ProcessNextEntry();
    }

    private bool ProcessNextEntry()
    {
        // Loop instead of recursion to avoid stack overflow on deep trees
        while (true)
        {
            if (!EnsureChildEnumeratorInitialized())
            {
                return false; // No more entries to process
            }

            if (TryMoveToNextChild())
            {
                return true; // Successfully moved to next entry
            }

            // Current enumerator exhausted, reset and continue with next parent
            _childEnumerator = null;
        }
    }

    private bool EnsureChildEnumeratorInitialized()
    {
        if (_childEnumerator != null)
        {
            return true;
        }

        if (_entries.Count == 0)
        {
            return false;
        }

        var parent = _entries.Pop();
        _childEnumerator = parent.Children.GetEnumerator();
        return true;
    }

    private bool TryMoveToNextChild()
    {
        if (!_childEnumerator!.MoveNext())
        {
            return false;
        }

        _current = _childEnumerator.Current;

        if (ShouldPushToStack(_current))
        {
            _entries.Push(_current);
        }

        return true;
    }

    private static bool ShouldPushToStack(ICommonEntry entry)
    {
        return entry.IsDirectory && entry.Children is { Count: > 0 };
    }

    public void Reset()
    {
        _current = null;
        if (_entries != null)
        {
            CollectionPool.ReturnCommonEntryStack(_entries);
        }
        _entries = StackOfRoots(_rootEntries);
        _childEnumerator = null;
    }

    IEnumerator<ICommonEntry> IEnumerable<ICommonEntry>.GetEnumerator()
    {
        return new DirEntryEnumerator(_rootEntries);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new DirEntryEnumerator(_rootEntries);
    }
}