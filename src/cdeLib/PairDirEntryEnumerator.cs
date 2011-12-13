using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace cdeLib
{
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

        private const CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
        private readonly CompareInfo _myCompareInfo = CompareInfo.GetCompareInfo("en-US");

        public bool MoveNext()
        {
            _current = null;
            if (_childEnumerator == null)
            {
                if (_entries.Count > 0)
                {
                    var de = _entries.Pop();

                    _parentDirEntry = de;
                    // 
                    // TODO - RootEntry needs All the Flags.
                    // TODO - maybe RootEntry derives from DirEntry ? collapse CE and DE maybe ?
                    // 
                    // Use DefaultSorted Field to only do when needed ? use a bit field ?
                    de.Children.Sort((de1, de2) =>
                        {   // Dir then File, then by Path, using culture and options.
                            if (de1.IsDirectory && !de2.IsDirectory)
                            {
                                return -1; // de1 before de2
                            }
                            if (!de1.IsDirectory && de2.IsDirectory)
                            {
                                return 1; // de1 after de2
                            }
                            return _myCompareInfo.Compare(de1.Path, de2.Path, MyCompareOptions);
                            //return de1.Path.CompareTo(de2.Path);
                        });
                    _childEnumerator = de.Children.GetEnumerator();
                }
            }

            if (_childEnumerator != null)
            {
                if (_childEnumerator.MoveNext())
                {
                    var de = _childEnumerator.Current;
                    _current = new PairDirEntry(_parentDirEntry, de);
                    if (de.IsDirectory && de.Children != null && de.Children.Count > 0)
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