using System;
using System.Collections.Generic;
using System.IO;
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib
{
    public class EntryStore
    {
        private const int  ShiftMaskBit = 19;
        private const uint BlockMask = 0x7FFFF; //0xFFF80000;
        private const uint BlockSize = 524288;  // 2 ^ 19 = 524288
        private const uint MaxBlocks = 1000;    // 500 million entries. is a lot.
        public static uint _current = 1;      // zero is always empty.
        public static object[] _block = new object[MaxBlocks];

        //[ProtoMember(5, IsRequired = true)]
        // his is per root lol!
        //public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        public static List<RootEntry> RootEntries  = new List<RootEntry>();

        public static void Reset()
        {
            _current = 1;
            _block = new object[MaxBlocks];
        }

        public Entry this[uint myEntryIndex]
        {
            get
            {
                var index = myEntryIndex & BlockMask;
                var blockI = myEntryIndex >> ShiftMaskBit;
                var block = (Entry[])_block[blockI];
                if (block == null)
                {
                    block = new Entry[BlockSize];
                    _block[blockI] = block;
                }
                return block[index];
            }
        }

        public static uint NewRoot(string path)
        {
            var myCurrent = _current;
            ++_current;
            var index = myCurrent & BlockMask;
            var blockI = myCurrent >> ShiftMaskBit;
            var block = (Entry[])_block[blockI];
            if (block == null)
            {
                block = new Entry[BlockSize];
                _block[blockI] = block;
            }

            // get a root entry
            var newRoot = new RootEntry();
            newRoot.GetRootEntry(path);
            newRoot.RootIndex = myCurrent;
            RootEntries.Add(newRoot);

            block[index].FullPath = newRoot.RootPath;
            block[index].Parent = 0;
            block[index].IsDirectory = true;

            return myCurrent;
        }

        /// <summary>
        /// Not Thread Safe.
        /// </summary>
        public static uint Add(FileSystemEntryInfo fs)
        {
            var myCurrent = _current;
            ++_current;
            var index = myCurrent & BlockMask;
            var blockI = myCurrent >> ShiftMaskBit;
            var block = (Entry[])_block[blockI];
            if (block == null)
            {
                block = new Entry[BlockSize];
                _block[blockI] = block;
            }
            block[index].Set(fs);
            return myCurrent;
        }

        public void BuildThisFrom(RootEntry root)
        {
            // duplicate a full tree into this ?
            // simpler to load a tree to RootTree with scan...
            // then load a tree again into this with scan... i think
            
        }

        const string MatchAll = "*";

        public static void RecurseTree(RootEntry root)
        {
            var entryCount = 0;
            var dirs = new Stack<Tuple<uint, string>>();
            dirs.Push(Tuple.Create(root.RootIndex, root.RootPath));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var parentEntryIndex = t.Item1;
                var directory = t.Item2;

                var parentIndex = parentEntryIndex & BlockMask;
                var parentBlockI = parentEntryIndex >> ShiftMaskBit;
                var parentBlock = (Entry[])_block[parentBlockI];
                if (parentBlock == null)
                {
                    parentBlock = new Entry[BlockSize];
                    _block[parentBlockI] = parentBlock;
                }
                uint siblingIndex = 0; // entering a directory again

                var fsEntries = Directory.GetFullFileSystemEntries
                    (null, directory, MatchAll, SearchOption.TopDirectoryOnly, false, exceptionHandler, null);
                foreach (var fsEntry in fsEntries)
                {
                    //var dirEntry = new DirEntry(fsEntry);
                    //commonEntry.Children.Add(dirEntry);
                    var newEntryIndex = Add(fsEntry);

                    var index = newEntryIndex & BlockMask;
                    var blockI = newEntryIndex >> ShiftMaskBit;
                    var block = (Entry[])_block[blockI];
                    if (block == null)
                    {
                        block = new Entry[BlockSize];
                        _block[blockI] = block;
                    }

                    block[index].Parent = parentEntryIndex;
                    if (siblingIndex == 0)
                    {
                        parentBlock[parentIndex].Child = newEntryIndex; // first of siblings is child
                    }
                    else
                    {
                        block[index].Sibling = siblingIndex;
                    }
                    siblingIndex = newEntryIndex; // sibling chain for next entry
                    if (block[index].IsBadModified)
                    {
                        Console.WriteLine("Bad date on \"{0}\"", fsEntry.FullPath);
                    }

                    if (fsEntry.IsDirectory)
                    {
                        block[index].FullPath = fsEntry.FullPath; 
                        dirs.Push(Tuple.Create(newEntryIndex, fsEntry.FullPath));
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

        public static int EntryCountThreshold { get; set; }

        public static Action SimpleScanCountEvent { get; set; }

        public static Action SimpleScanEndEvent { get; set; }

        private static EnumerationExceptionDecision exceptionHandler(string path, Exception e)
        {
            if (e.GetType().Equals(typeof(UnauthorizedAccessException)))
            {
                //PathsWithUnauthorisedExceptions.Add(path);
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