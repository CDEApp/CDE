﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace cdeDataStructure3.Entities
{
    public class PairDirEntryEnumerator : IEnumerator<PairDirEntry>, IEnumerable<PairDirEntry>
    {
        private readonly IEnumerable<RootEntry> _rootEntries;
        private PairDirEntry _current;
        private Stack<CommonEntry> _entries;
        private CommonEntry _parentDirEntry;
        private IEnumerator<DirEntry> _childEnumerator;
        private bool _isDisposed;

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

        private static Stack<CommonEntry> StackOfRoots(IEnumerable<RootEntry> rootEntries)
        {
            var entries = new Stack<CommonEntry>();
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
           Dispose(true);
           GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                _current = null;
                _entries = null;
            }

            _isDisposed = true;
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
                    if (de.IsDirectory && de.Children is { Count: > 0 })
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
}