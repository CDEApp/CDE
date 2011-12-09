using System.Collections;
using System.Collections.Generic;

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
                    _current = _childEnumerator.Current;
                    if (_current.IsDirectory)
                    {
                        _entries.Push(_current); // TODO dont push if no children. ? ie 0 or null
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
}