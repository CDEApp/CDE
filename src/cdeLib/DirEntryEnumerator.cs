﻿using System.Collections;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    public class DirEntryEnumerator : IEnumerator<DirEntry>, IEnumerable<DirEntry> // is it weird to be both
    {
        private readonly IEnumerable<RootEntry> _rootEntries;
        private DirEntry _current;
        private Stack<CommonEntry> _entries;
        private IEnumerator<DirEntry> _childEnumerator;

        public DirEntry Current
        {
            get { return _current; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

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

        private static Stack<CommonEntry> StackOfRoots(IEnumerable<RootEntry> rootEntries)
        {
            var entries = new Stack<CommonEntry>();
            foreach (var re in rootEntries)
            {
                entries.Push(re);
            }
            return entries;
        }

        public void Dispose()
        {
            _current = null;
            _entries = null;
        }

        // idea that I can somehow Concat() iterators from the Children to _childEnumerator - dont think so now.
        public bool MoveNext()
        {
            _current = null;
            if (_childEnumerator == null)
            {
                if (_entries.Count > 0)
                {
                    var de = _entries.Pop();
                    _childEnumerator = de.Children.GetEnumerator();
                }
            }

            if (_childEnumerator != null)
            {
                if (_childEnumerator.MoveNext())
                {
                    _current = _childEnumerator.Current;
                    if (_current.IsDirectory)
                    {
                        _entries.Push(_current);
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

        IEnumerator<DirEntry> IEnumerable<DirEntry>.GetEnumerator()
        {
            return new DirEntryEnumerator(_rootEntries);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new DirEntryEnumerator(_rootEntries);
        }
    }

    public class PairDirEntry
    {
        public CommonEntry ParentDE;
        public DirEntry ChildDE;

        public string FilePath
        {
            get
            {
                var a = ParentDE.FullPath ?? "<<pnull>>";
                var b = ChildDE.Name ?? "<<dnull>>";
                return Path.Combine(a, b);
            }
        }

        public PairDirEntry(CommonEntry parent, DirEntry child)
        {
            ParentDE = parent;
            ChildDE = child;
        }
    }

    public class PairDirEntryEnumerator : IEnumerator<PairDirEntry>, IEnumerable<PairDirEntry>
    {
        private readonly IEnumerable<RootEntry> _rootEntries;
        private PairDirEntry _current;
        private Stack<CommonEntry> _entries;
        private CommonEntry _parentDirEntry;
        private IEnumerator<DirEntry> _childEnumerator;

        public PairDirEntry Current
        {
            get { return _current; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

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
                entries.Push(re);
            }
            return entries;
        }

        public void Dispose()
        {
            _current = null;
            _entries = null;
        }

        // idea that I can somehow Concat() iterators from the Children to _childEnumerator - dont think so now.
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
                    if (de.IsDirectory)
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