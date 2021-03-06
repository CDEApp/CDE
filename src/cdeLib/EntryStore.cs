﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileMode = System.IO.FileMode;
//using FileSystemInfo = Alphaleonis.Win32.Filesystem.FileSystemInfo;

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

        // could do our own array like data structure for BaseBlock maybe ?
        [ProtoMember(1, IsRequired = true)]
        private SortedList<int, Entry[]> BaseBlock;

        public int BaseBlockCount { get { return BaseBlock.Count; } }

        [ProtoMember(2, IsRequired = true)]
        public RootEntry Root;

        [ProtoMember(3, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public int NextAvailableIndex;

        public EntryStore()
        {
            Reset();
        }

        private void Reset()
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

        // todo use
        // hand out what i have from EntryIndex, should make callers not need to call EntryIndex.
        public int AddEntry(out int blockIndex, out Entry[] block)
        {
            var myNewIndex = NextAvailableIndex;
            ++NextAvailableIndex;
            blockIndex = EntryIndex(myNewIndex, out block); // ensure block allocated.
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
                throw new IndexOutOfRangeException("Entry index exceeds Entry block length.");
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

            block[entryIndex].FullPath = Root.Path;
            block[entryIndex].Parent = 0;
            block[entryIndex].IsDirectory = true;
            return myNewIndex;
        }

        const string MatchAll = "*";

        // Better name ScanRootPath.
        // -- consider each breadth first scan, sort then add to the Store, [dont do one at time].
        public void RecurseTree()
        {
            if (Root == null)
            {
                throw new ArgumentException("Root is not set.");
            }
            var entryCount = 0;
            var dirs = new Stack<Tuple<int, string>>();
            dirs.Push(Tuple.Create(Root.RootIndex, Root.Path));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var parentIndex = t.Item1;
                var directory = t.Item2;

                Entry[] parentBlock;
                var parentEntryIndex = EntryIndex(parentIndex, out parentBlock);
                int siblingIndex = 0; // entering a directory again

                // NEW
                //var fsEntries = new FindFileSystemEntryInfo
                //    {
                //        IsFullPath = true,
                //        InputPath = directory,
                //        AsLongPath = true,
                //        GetFsoType = null, // both files and folders.
                //        SearchOption = SearchOption.TopDirectoryOnly,
                //        SearchPattern = MatchAll,
                //        Transaction = null,
                //        ContinueOnAccessError = true // ignoring them all, cant collec them like use to.
                //    }.Enumerate();
                var fsEntries = Directory.EnumerateFileSystemEntryInfos<FileSystemEntryInfo>(directory, MatchAll, DirectoryEnumerationOptions.FilesAndFolders); // was SearchOption.TopDirectoryOnly is this a match?   Recursive is another flag that can be added, can we assume then top only


                // OLD
                //var fsEntries = Directory.GetFullFileSystemEntries
                //    (null, directory, MatchAll, SearchOption.TopDirectoryOnly, false, exceptionHandler, null);
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
                        Entry[] siblingBlock;
                        var siblingEntryIndex = EntryIndex(siblingIndex, out siblingBlock);
                        siblingBlock[siblingEntryIndex].Sibling = newIndex;
                    }
                    siblingIndex = newIndex; // sibling chain for next entry
                    if (block[entryIndex].IsModifiedBad)
                    {
                        Console.WriteLine($"Bad date on \"{fsEntry.FullPath}\"");
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
        public void OptimiseEntryStoreSize()
        {
            var highestIndex = NextAvailableIndex;
            var blockIndex = highestIndex >> ShiftMaskBit;
            var highestEntryIndex = (int)(highestIndex & BlockMask);
            if (blockIndex < BaseBlock.Count)   // if block not allocated dont optimise it.
            {
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
                    entryStore.SetInMemoryFields();
                    entryStore.SetSummaryFields();
                    return entryStore;
                }
            }
            return null;
        }

        public static List<EntryStore> LoadCurrentDirCache()
        {
            var roots = new List<EntryStore>();
            var files = AlphaFSHelper.GetFilesWithExtension(".", "cde");
            foreach (var file in files)
            {
                var re = Read(file);
                if (re != null)
                {
                    roots.Add(re);
                }
            }
            return roots;
        }

        public static EntryStore Read(Stream input)
        {
            return Serializer.Deserialize<EntryStore>(input);
        }

        // convenience, may flesh out with all fields.
        public int AddEntry(string name, string fullpath, ulong size, DateTime modified, bool isDirectory = false, int parentIndex = 0)
        {
            var myNewIndex = AddEntry();
            Entry[] block;
            var entryIndex = EntryIndex(myNewIndex, out block);
            block[entryIndex].Name = name;
            block[entryIndex].FullPath = fullpath;
            block[entryIndex].Size = size;
            block[entryIndex].Modified = modified;
            block[entryIndex].IsDirectory = isDirectory;

            if (parentIndex > 0)
            {
                block[entryIndex].Parent = parentIndex;
                Entry[] parentBlock;
                var parentEntryIndex = EntryIndex(parentIndex, out parentBlock);
                var currentFirstChildIndex = parentBlock[parentEntryIndex].Child;
                if (currentFirstChildIndex != 0)
                {   // prepend our new Entry to the sibling chain
                    block[entryIndex].Sibling = currentFirstChildIndex;
                }
                parentBlock[parentEntryIndex].Child = myNewIndex;
            }
            return myNewIndex;
        }

        // useful, but RecurseTree has this inline, [its a bit more efficient but arguably not worth it]
        public int AddEntry(FileSystemEntryInfo fs, int parentIndex = 0, int siblingIndex = 0)
        {
            var myNewIndex = AddEntry();
            Entry[] block;
            var entryIndex = EntryIndex(myNewIndex, out block);
            block[entryIndex].Set(fs);

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

        public void SetSummaryFields()
        {
            //var dirStats = new CommonEntry.DirStats();
            //SetSummaryFieldsXX(dirStats);
            //Root.DirCount = dirStats.DirCount;
            //Root.FileCount = dirStats.FileCount;
        }

        public void SetSummaryFieldsXX()
        {
            // do i need a recursive version here can i use iterator ?
            // if i check path changes iterator might work ? 
            // i know iterator is current entries first then down to first subdir.

            // enumerator is breadth first, so adding up files/dirs works.
            // but adding up sizes at each dir in tree isnt as simple...

            var entryEnumerator = new EntryEnumerator(this);
            var prevParentPath = string.Empty;
            var size = 0ul;
            var block = new Entry[] {};
            int entryIndex = -1;
            foreach (var entryKey in entryEnumerator)
            {
                entryIndex = EntryIndex(entryKey.Index, out block);
                string currParentPath = block[entryIndex].GetParentPath(this);
                //if (block[entryIndex].IsDirectory)
                //{
                //    dirStats.DirCount += 1;
                //}
                //else
                //{
                //    dirStats.FileCount += 1;
                //}
                if (currParentPath == prevParentPath)
                {
                    if (!block[entryIndex].IsDirectory)
                    {
                        size += block[entryIndex].Size;
                    }
                }
                else
                {
                    block[entryIndex].SetParentSize(this, size);
                    size = 0ul;
                }

                prevParentPath = currParentPath;
            }
            if (entryIndex >= 0) // catch the setting after whole tree processed.
            {
                block[entryIndex].SetParentSize(this, size);
            }
        }

        // Set FullPath on all IsDirectory fields in store.
        // This relies on enumerator being breadth first.
        public void SetInMemoryFields()
        {
            var entryEnumerator = new EntryEnumerator(this);
            foreach (var entryKey in entryEnumerator)
            {
                Entry[] block;
                var entryIndex = EntryIndex(entryKey.Index, out block);
                if (block[entryIndex].IsDirectory)
                {   // set full path on this dir
                    block[entryIndex].FullPath = block[entryIndex].GetFullPath(this);
                }
            }
        }

        public void IsValid()
        {
            if (Root == null)
            {
                throw new Exception("Entry Store must have Root set to be valid.");
            }
            if (Root.RootIndex == 0)
            {
                throw new Exception("Entry Store Root must have valid RootIndex.");
            }
            if (string.IsNullOrEmpty(Root.Path))
            {
                throw new Exception("Entry Store Root must have valid Path.");
            }
            if (string.IsNullOrEmpty(Root.DefaultFileName))
            {
                throw new Exception("Entry Store Root must have valid DefaultFileName.");
            }

            Entry[] block;
            var rootEntryIndex = EntryIndex(Root.RootIndex, out block);

            if (!block[rootEntryIndex].IsDirectory)
            {
                throw new Exception("Entry Store Root Index Entry must be a directory.");
            }
            if (string.IsNullOrEmpty(block[rootEntryIndex].FullPath))
            {
                throw new Exception("Entry Store Root Index Entry must have non empty FullPath set.");
            }
        }

        public static void PrintPathsHaveHash()
        {
            var rootEntries = LoadCurrentDirCache();
            foreach (var entryStore in rootEntries)
            {
                entryStore.PrintPathsHaveHash2();
            }
        }

        public void PrintPathsHaveHash2()
        {
            var entryEnumerator = new EntryEnumerator(this);
            foreach (var entryKey in entryEnumerator)
            {
                Entry[] block;
                var entryIndex = EntryIndex(entryKey.Index, out block);
                var hash = block[entryIndex].IsHashDone ? "#" : " ";
                Console.WriteLine($"{hash}{block[entryIndex].GetFullPath(this)}");
            }
        }

        private Logger _logger;
        private ApplicationDiagnostics _applicationDiagnostics;
        private IDictionary<ulong, List<int>> _duplicateFileSize;

        public void ApplyMd5Checksum()
        {
            _logger = new Logger();
            _applicationDiagnostics = new ApplicationDiagnostics();

            _logger.LogDebug("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var sizeDupes = GetSizePairs();
            _logger.LogDebug("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        }

        public IDictionary<ulong, List<int>> GetSizePairs()
        {
            // try out idea of processing all in slices removing 1's each time.
            ulong bumpSize = 20000;// +200000000000;
            //bumpSize = 50000  +200000000000;
            ulong min = 0;
            ulong max = bumpSize;
            bool goNext;
            int loopy = 0;
            _duplicateFileSize = new Dictionary<ulong, List<int>>();

            //Console.WriteLine(String.Format("Post TraverseMatchOnFileSize: {0}, dupeDictCount {1}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize.Count));
            do
            {
                goNext = false;
                var entryEnumerator = new EntryEnumerator(this);
                foreach (var entryKey in entryEnumerator)
                {
                    Entry[] block;
                    var index = entryKey.Index;
                    var entryIndex = EntryIndex(index, out block);
                    var size = block[entryIndex].Size;

                    if (size >= min && size < max)
                    {
                        if (!block[entryIndex].IsDirectory && size != 0)
                        {
                            if (_duplicateFileSize.ContainsKey(size))
                            {
                                _duplicateFileSize[size].Add(index);
                            }
                            else
                            {
                                _duplicateFileSize[size] = new List<int> { index };
                            }
                        }
                    }
                    else if (size >= max)
                    {
                        goNext = true;
                    }
                }

                //Remove the single values from the dictionary.
                var pruneList = _duplicateFileSize.Where(kvp => kvp.Value.Count == 1)
                    .ToList();
                //Console.WriteLine("Prune 1's {0}", pruneList.Count);
                pruneList.ForEach(x => _duplicateFileSize.Remove(x.Key));

                if (goNext)
                {
                    min = max;
                    max += bumpSize;
                    if (min > 2000000)
                    {
                        bumpSize = bumpSize + bumpSize;
                    }
                    //bumpSize *= (ulong)(bumpSize * 1.5);
                    //bumpSize = bumpSize + bumpSize;
                }
                ++loopy;
                //Console.WriteLine("loopy {0} min {1} max {2}", loopy, min, max);
                //GC.Collect();

                if (Hack.BreakConsoleFlag)
                {
                    break;
                }
            } while (goNext);

            Console.WriteLine($"loopy {loopy}");
            Console.WriteLine(
                $"Deleted entries from dictionary: {_applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()}, dupeDictCount {_duplicateFileSize.Count}");
            return _duplicateFileSize;
        }
    }
}