﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace cdeLib
{
    public class EntryKey
    {
        public int Index; // external index
        //public Entry[] Block;   // derivable from Index
        //public int EntryIndex;  // derivable from Index
    }

    public class EntryEnumerator : IEnumerator<EntryKey> //, IEnumerable<EntryKey>
    {
        private readonly IEnumerable<EntryStore> _entryStores;
        //private readonly IEnumerable<EntryKey> _entryKeys;
        private Stack<int> _indexStack;
        private EntryKey _current;
        private EntryStore _entryStore;

        public EntryEnumerator(EntryStore entryStore)
        {
            //_entryStores = new List<EntryStore> { entryStore };
            _indexStack = new Stack<int>();
            _indexStack.Push(entryStore.Root.RootIndex);
            _entryStore = entryStore;
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

        public EntryEnumerator(int rootIndex, EntryStore entryStore)
        {
            _indexStack = new Stack<int>();
            _indexStack.Push(rootIndex);
            Reset();
        }

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
                    var dirIndex = _indexStack.Pop();
                    Entry[] dirBlock;
                    var dirEntryIndex = _entryStore.EntryIndex(dirIndex, out dirBlock);

                    if (dirBlock != null)
                    {
                        if (dirBlock[dirEntryIndex].Child != 0)
                        {
                            _current = new EntryKey {Index = dirBlock[dirEntryIndex].Child};
                        }
                    }
                }
            }
            else
            {
                Entry[] currentBlock;
                var currentEntryIndex = _entryStore.EntryIndex(_current.Index, out currentBlock);
                if (currentBlock[currentEntryIndex].Child != 0) // should i check IsDirectory ?
                {
                    _indexStack.Push(_current.Index);
                }

                if (currentBlock[currentEntryIndex].Sibling != 0)
                {
                    _current = new EntryKey {Index = currentBlock[currentEntryIndex].Sibling};
                }
                else
                {
                    if (currentBlock[currentEntryIndex].Child != 0)
                    {
                        _current = new EntryKey {Index = currentBlock[currentEntryIndex].Child};
                    }
                    else
                    {
                        _current = null;
                    }
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
    }
}