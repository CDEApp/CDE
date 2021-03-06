﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace cdeLib
{
    [DebuggerDisplay("Index = {Index}")]
    public class EntryKey
    {
        public int Index; // external index
        //public Entry[] Block;   // derivable from Index
        //public int EntryIndex;  // derivable from Index
    }

    /// <summary>
    /// This enumerates path nodes breadth first.
    /// </summary>
    [DebuggerDisplay("_current = {_current.Index} stack {_indexStack.Count}")]
    public class EntryEnumerator : IEnumerator<EntryKey>, IEnumerable<EntryKey>
    {
        //private readonly IEnumerable<EntryStore> _entryStores;
        //private readonly IEnumerable<EntryKey> _entryKeys;
        private Queue<int> _indexStack; // using Queue so we enter dirs in order of hitting them.
        private EntryKey _current;
        private readonly EntryStore _entryStore;

        private readonly EntryKey _cachedEntryKey; // our key for returning enumerator state.

        public EntryEnumerator(EntryStore entryStore)
        {
            //_entryStores = new List<EntryStore> { entryStore };
            _indexStack = new Queue<int>();
            _indexStack.Enqueue(entryStore.Root.RootIndex);
            _entryStore = entryStore;
            _cachedEntryKey = new EntryKey();
            StoresAreValid();
            Reset();
        }

        //public EntryEnumerator(IEnumerable<EntryStore> entryStores)
        //{
        //    _entryStores = entryStores;
        //    //_dirStack = new Stack<int>();
        //    //_dirStack.Push(rootIndex);
        //    StoresAreValid();
        //    Reset();
        //}

        private void StoresAreValid()
        {
            //foreach (var entryStore in _entryStores)
            //{
               _entryStore.IsValid();
            //}
        }

        //public EntryEnumerator(int rootIndex, EntryStore entryStore)
        //{
        //    _indexStack = new Stack<int>();
        //    _indexStack.Push(rootIndex);
        //    Reset();
        //}

        public void Dispose()
        {
            _current = null;
            _indexStack = null;
        }

        public bool MoveNext()
        {
            if (_current == null)
            {
                if (_indexStack.Count > 0)
                {
                    var dirIndex = _indexStack.Dequeue();
                    Entry[] dirBlock;
                    var dirEntryIndex = _entryStore.EntryIndex(dirIndex, out dirBlock);

                    if (dirBlock != null && dirBlock[dirEntryIndex].Child != 0)
                    {
                        _cachedEntryKey.Index = dirBlock[dirEntryIndex].Child;
                        _current = _cachedEntryKey; //_current = new EntryKey {Index = dirBlock[dirEntryIndex].Child};
                    }
                }
            }
            else
            {
                Entry[] currentBlock;
                var currentEntryIndex = _entryStore.EntryIndex(_current.Index, out currentBlock);

                if (currentBlock[currentEntryIndex].Child != 0) // should i check IsDirectory ?
                {
                    _indexStack.Enqueue(_current.Index);
                }

                if (currentBlock[currentEntryIndex].Sibling != 0)
                {
                    _cachedEntryKey.Index = currentBlock[currentEntryIndex].Sibling;
                    _current = _cachedEntryKey; //_current = new EntryKey { Index = currentBlock[currentEntryIndex].Sibling };
                }
                else
                {
                    _current = null;
                }

                if (_current == null)
                {
                    return MoveNext();
                }
            }
            return _current != null;
        }

        public void Reset()
        {
            _current = null;
        }

        public EntryKey Current
        {
            get { return _current; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public IEnumerator<EntryKey> GetEnumerator()
        {
            return new EntryEnumerator(_entryStore);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EntryEnumerator(_entryStore);
        }
    }
}