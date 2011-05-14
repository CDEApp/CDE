﻿using System;
using System.Collections.Generic;
using System.IO;
using Alphaleonis.Win32.Filesystem;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileMode = Alphaleonis.Win32.Filesystem.FileMode;

namespace cdeLib
{
    [ProtoContract]
    public class EntryStore
    {
        // 2 ^ 16 = 65536 Entry per block.
        // 256*65535 = 16 million . with 2048 bytes on base array.
        // NextAvailableIndex starts at 1. zero is always empty block, it represents parent of roots.
        private const int ShiftMaskBit = 16;
        private const uint BlockMask = 0xFFFF;
        private const uint BlockSize = 65536;       

        public int NextAvailableIndex;               // not need to save ?

        // could do our own array like data structure for BaseBlock maybe ?
        [ProtoMember(1, IsRequired = true)]
        SortedList<int, Entry[]> BaseBlock;

        public int BaseBlockCount { get { return BaseBlock.Count; } }

        [ProtoMember(2, IsRequired = true)]
        public RootEntry Root;

        [ProtoMember(3, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        public EntryStore()
        {
            Reset();
        }

        public void Reset()
        {
            NextAvailableIndex = 1;  // First Index to hold data.
            BaseBlock = new SortedList<int, Entry[]>(10);
            PathsWithUnauthorisedExceptions = new List<string>();
        }

        /// <summary>
        /// Convenience version.
        /// </summary>
        public int AddEntry()
        {
            var myNewIndex = NextAvailableIndex;
            ++NextAvailableIndex;
            Entry[] block;
            EntryIndex(myNewIndex, out block); // ensure block allocated.
            return myNewIndex;
        }

        /// <summary>
        /// Converts Index to a <paramref name="block"/> 
        /// and returns entryIndex in that block to the entry.
        /// Allocates Entry array if it is not allready allocated.
        /// </summary>
        public int EntryIndex(int index, out Entry[] block)
        {
            if (index >= NextAvailableIndex)
            {
                throw new IndexOutOfRangeException("Out of allocated Entry store range.");
            }
            var blockIndex = index >> ShiftMaskBit;
            var entryIndex = (int)(index & BlockMask);
            if (blockIndex == BaseBlock.Values.Count) // sorted list array quicker than lookup via key ?
            {
                block = new Entry[BlockSize];
                BaseBlock[blockIndex] = block;
            }
            else
            {
                block = BaseBlock.Values[blockIndex]; // sorted list array quicker than lookup via key ?
            }

            if (entryIndex > block.Length)
            {
                throw new IndexOutOfRangeException("Entry index exceedsd Entry block length.");
                // this happens if we shorten the last block to length needed.
                // - hackery at end of scan truncate last array to only required size.
            }
            return entryIndex;
        }

        public int SetRoot(string path)
        {
            var myNewIndex = AddEntry();
            Entry[] block;
            var entryIndex = EntryIndex(myNewIndex, out block);

            Root = new RootEntry();
            Root.GetRootEntry(path);
            Root.RootIndex = myNewIndex;

            block[entryIndex].FullPath = Root.RootPath;
            block[entryIndex].Parent = 0;
            block[entryIndex].IsDirectory = true;
            return myNewIndex;
        }

        const string MatchAll = "*";

        public void RecurseTree()
        {
            if (Root == null)
            {
                throw new ArgumentException("Root is not set.");
            }
            var entryCount = 0;
            var dirs = new Stack<Tuple<int, string>>();
            dirs.Push(Tuple.Create(Root.RootIndex, Root.RootPath));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var parentIndex = t.Item1;
                var directory = t.Item2;

                Entry[] parentBlock;
                var parentEntryIndex = EntryIndex(parentIndex, out parentBlock);
                int siblingIndex = 0; // entering a directory again

                var fsEntries = Directory.GetFullFileSystemEntries
                    (null, directory, MatchAll, SearchOption.TopDirectoryOnly, false, exceptionHandler, null);
                foreach (var fsEntry in fsEntries)
                {
                    var newIndex = AddEntry();
                    Entry[] block;
                    var entryIndex = EntryIndex(newIndex, out block);

                    block[entryIndex].Set(fsEntry);

                    block[entryIndex].Parent = parentIndex;
                    if (siblingIndex == 0)
                    {
                        parentBlock[parentEntryIndex].Child = newIndex; // first of siblings is child
                    }
                    else
                    {
                        block[entryIndex].Sibling = siblingIndex;
                    }
                    siblingIndex = newIndex; // sibling chain for next entry
                    if (block[entryIndex].IsBadModified)
                    {
                        Console.WriteLine("Bad date on \"{0}\"", fsEntry.FullPath);
                    }

                    if (fsEntry.IsDirectory)
                    {
                        // not required for saving - can we custom clear a field to not save?
                        // hmm dont save FullPath maybe ?
                        //block[entryIndex].FullPath = fsEntry.FullPath;
                        dirs.Push(Tuple.Create(newIndex, fsEntry.FullPath));
                    }
                    ++entryCount;
                    if (entryCount > EntryCountThreshold)
                    {
                        if (SimpleScanCountEvent != null)
                        {
                            SimpleScanCountEvent();
                        }
                        entryCount = 0;
                    }
                }
            }
            if (SimpleScanEndEvent != null)
            {
                SimpleScanEndEvent();
            }
        }

        public int EntryCountThreshold { get; set; }

        public Action SimpleScanCountEvent { get; set; }

        public Action SimpleScanEndEvent { get; set; }

        private EnumerationExceptionDecision exceptionHandler(string path, Exception e)
        {
            if (e.GetType().Equals(typeof(UnauthorizedAccessException)))
            {
                PathsWithUnauthorisedExceptions.Add(path);
                return EnumerationExceptionDecision.Skip;
            }

            if (ExceptionEvent != null)
            {
                ExceptionEvent(path, e);
            }
            return EnumerationExceptionDecision.Abort;
        }

        public static Action<string, Exception> ExceptionEvent { get; set; }

        public void SaveToFile()
        {
            OptimiseEntryStoreSize();

            if (Root == null)
            {
                throw new Exception("Root must be set on EntryStore to Save.");
            }
            using (var newFs = File.Open(Root.DefaultFileName, FileMode.Create))
            {
                Write(newFs);
            }
        }

        /// <summary>
        /// This reduces the size of last block to minimum needed to hold those entries.
        /// </summary>
        private void OptimiseEntryStoreSize()
        {
            var highestIndex = NextAvailableIndex;
            var blockIndex = highestIndex >> ShiftMaskBit;
            var highestEntryIndex = (int)(highestIndex & BlockMask);
            var block = BaseBlock.Values[blockIndex];
            if (block != null)
            {
                if (highestEntryIndex < block.Length)
                {   // reduce our last block size to minimum required.
                    Array.Resize(ref block, highestEntryIndex);
                    BaseBlock[blockIndex] = block;
                }
            }
        }

        public void Write(Stream output)
        {
            Serializer.Serialize(output, this);
        }

        public static EntryStore Read(string file)
        {
            if (File.Exists(file))
            {
                using (var fs = File.Open(file, FileMode.Open))
                {
                    var entryStore = Read(fs);
                    //re.SetInMemoryFields();
                    return entryStore;
                }
            }
            return null;
        }

        public static EntryStore Read(Stream input)
        {
            return Serializer.Deserialize<EntryStore>(input);
        }

        //public int AddEntry(string name, string fullpath, ulong size, DateTime modified, bool isDirectory = false)
        //{
        //    var myNewIndex = AddEntry();
        //    Entry[] block;
        //    var entryIndex = EntryIndex(myNewIndex, out block);
        //    block[entryIndex].Name = name;
        //    block[entryIndex].FullPath = fullpath;
        //    block[entryIndex].Size = size;
        //    block[entryIndex].Modified = modified;
        //    return myNewIndex;
        //}

        public int AddEntry(string name, string fullpath, ulong size, DateTime modified, bool isDirectory = false, int parentIndex = 0, int siblingIndex = 0)
        {
            var myNewIndex = AddEntry();
            Entry[] block;
            var entryIndex = EntryIndex(myNewIndex, out block);
            block[entryIndex].Name = name;
            block[entryIndex].FullPath = fullpath;
            block[entryIndex].Size = size;
            block[entryIndex].Modified = modified;

            if (parentIndex > 0)
            {
                block[entryIndex].Parent = parentIndex;

                Entry[] parentBlock;
                var parentEntryIndex = EntryIndex(parentIndex, out parentBlock);
                parentBlock[parentEntryIndex].Child = myNewIndex;
            }

            if (siblingIndex > 0)
            {
                block[entryIndex].Sibling = siblingIndex;
            }
            return myNewIndex;
        }

    }
}