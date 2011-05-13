using System;
using System.Collections.Generic;
using System.IO;
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib
{
    public class EntryStore
    {
        // 2 ^ 16 = 65536
        // 256*65535 = 16 million . with 2048 bytes on base array.
        // NextAvailableIndex starts at 1. zero is always empty block, it represents parent of roots.
        private const int ShiftMaskBit = 16;
        private const uint BlockMask = 0xFFFF;
        private const uint BlockSize = 65536;       
        private const uint StartBaseBlocks = 256;   
        public const uint MaxIndex = StartBaseBlocks * BlockSize - 1;

        public uint NextAvailableIndex;               
        public uint BaseBlockCount;
        public object[] BaseBlock;

        public RootEntry Root;

        //[ProtoMember(5, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        public EntryStore()
        {
            Reset();
        }

        public void Reset()
        {
            NextAvailableIndex = 1;  // First Index to hold data.
            BaseBlockCount = StartBaseBlocks;
            BaseBlock = new object[StartBaseBlocks];
            PathsWithUnauthorisedExceptions = new List<string>();
        }

        /// <summary>
        /// Convenience version.
        /// </summary>
        public uint AddEntry()
        {
            if (NextAvailableIndex > MaxIndex)
            {
                throw new IndexOutOfRangeException(string.Format("Cannot allocate more entries reached maximum {0}", MaxIndex));
            }
            var myNewIndex = NextAvailableIndex;
            ++NextAvailableIndex;
            Entry[] block;
            EntryIndex(myNewIndex, out block); // ensure block allocated.
            return myNewIndex;
        }

        ///// <summary>
        ///// Allocate an Entry for use.
        ///// Not Thread Safe.
        ///// Pass out our block contexts for caller use.
        ///// Reallocates baseBlock as required. todo maybe
        ///// </summary>
        ///// <returns>The Index of allocated Entry.</returns>
        //public uint AddEntry(out uint blockIndex, out uint entryIndex)
        //{
        //    if (NextAvailableIndex > MaxIndex)
        //    {
        //        throw new IndexOutOfRangeException(string.Format("Cannot allocate more entries reached maximum {0}", MaxIndex));
        //    }
        //    var myNewIndex = NextAvailableIndex;
        //    ++NextAvailableIndex;
        //    entryIndex = EntryIndex(myNewIndex, out blockIndex); // ensure block allocated.
        //    return myNewIndex;
        //}

        ///// <summary>
        ///// Converts Index to a <paramref name="blockIndex"/> 
        ///// and returns entryIndex in that block to the entry.
        ///// Allocates Entry array if it is not allready allocated.
        ///// 
        ///// </summary>
        //public uint EntryIndex(uint index, out uint blockIndex)
        //{
        //    if (index >= NextAvailableIndex)
        //    {
        //        throw new IndexOutOfRangeException("Out of allocated Entry store range.");
        //    }
        //    blockIndex = index >> ShiftMaskBit;
        //    var entryIndex = index & BlockMask;
        //    var dataBlock = (Entry[])BaseBlock[blockIndex];
        //    if (dataBlock == null)
        //    {
        //        dataBlock = new Entry[BlockSize];
        //        BaseBlock[blockIndex] = dataBlock;
        //    }
        //    return entryIndex;
        //}

        /// <summary>
        /// Converts Index to a <paramref name="block"/> 
        /// and returns entryIndex in that block to the entry.
        /// Allocates Entry array if it is not allready allocated.
        /// </summary>
        public uint EntryIndex(uint index, out Entry[] block)
        {
            if (index >= NextAvailableIndex)
            {
                throw new IndexOutOfRangeException("Out of allocated Entry store range.");
            }
            var blockIndex = index >> ShiftMaskBit;
            var entryIndex = index & BlockMask;
            block = (Entry[])BaseBlock[blockIndex];
            if (block == null)
            {
                block = new Entry[BlockSize];
                BaseBlock[blockIndex] = block;
            }
            return entryIndex;
        }

        // NOT USEFUL. This data is a struct it just copies the fields out as value.
        //public Entry this[uint myEntryIndex]
        //{
        //    get
        //    {
        //        uint blockIndex;
        //        var entryIndex = EntryIndex(myEntryIndex, out blockIndex);
        //        return ((Entry[]) BaseBlock[blockIndex])[entryIndex];
        //    }
        //}

        public uint SetRoot(string path)
        {
            //var myNewIndex = AddEntry();
            //uint blockIndex;
            //var entryIndex = EntryIndex(BaseBlock, myEntryIndex, NextAvailableIndex, out blockIndex);

            var myNewIndex = NextAvailableIndex;

            ++NextAvailableIndex;
            var entryIndex = myNewIndex & BlockMask;
            var blockIndex = myNewIndex >> ShiftMaskBit;
            var dataBlock = (Entry[])BaseBlock[blockIndex];
            if (dataBlock == null)
            {
                dataBlock = new Entry[BlockSize];
                BaseBlock[blockIndex] = dataBlock;
            }

            // Initialise our Root details.
            Root = new RootEntry();
            Root.GetRootEntry(path);
            Root.RootIndex = myNewIndex;

            dataBlock[entryIndex].FullPath = Root.RootPath;
            dataBlock[entryIndex].Parent = 0;
            dataBlock[entryIndex].IsDirectory = true;

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
            var dirs = new Stack<Tuple<uint, string>>();
            dirs.Push(Tuple.Create(Root.RootIndex, Root.RootPath));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var parentIndex = t.Item1;
                var directory = t.Item2;

                Entry[] parentBlock;
                var parentEntryIndex = EntryIndex(parentIndex, out parentBlock);
                uint siblingIndex = 0; // entering a directory again

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

    }
}