using System.Collections;
using System.Collections.Generic;

namespace cdeDataStructure3.Entities
{
    public class DirEntryEnumerator : IEnumerator<DirEntry>, IEnumerable<DirEntry>
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
        }

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
                    if (_current.IsDirectory && _current.Children != null && _current.Children.Count > 0)
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
}