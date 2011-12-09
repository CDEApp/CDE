using System.Collections;
using System.Collections.Generic;

namespace cdeLib
{
    public class PairDirEntryEnumerator : IEnumerator<PairDirEntry>, IEnumerable<PairDirEntry>
    {
        private readonly IEnumerable<RootEntry> _rootEntries;
        private PairDirEntry _current;
        private Stack<CommonEntry> _entries;
        private RootEntry _rootEntry;
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
                entries.Push(re); // TODO dont push if no Children. ? ie 0 or null
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
                    if (de.IsRoot())
                    {
                        _rootEntry = (RootEntry)de;
                    }
                    _parentDirEntry = de;
                    if (de.Children != null)    // Children may not be initialized if Dir is empty.
                    {
                        _childEnumerator = de.Children.GetEnumerator();
                    }
                    else
                    {
                        _childEnumerator = null;
                        MoveNext();
                    }
                }
            }

            if (_childEnumerator != null)
            {
                if (_childEnumerator.MoveNext())
                {
                    var de = _childEnumerator.Current;
                    _current = new PairDirEntry(_parentDirEntry, de, _rootEntry);
                    if (de.IsDirectory)
                    {
                        _entries.Push(de); // TODO dont push if no Children. ? ie 0 or null
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