using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Hashing;

namespace cdeLib
{


    public class Duplication
    {
        private readonly IConfiguration _configuration;

        private readonly Dictionary<DirEntry, List<PairDirEntry>> _duplicateFile =
            new Dictionary<DirEntry, List<PairDirEntry>>(new DirEntry.EqualityComparer());

        private readonly Dictionary<long, List<PairDirEntry>> _duplicateFileSize =
            new Dictionary<long, List<PairDirEntry>>();

        private readonly HashSet<DirEntry> _dirEntriesRequiringFullHashing = new HashSet<DirEntry>();

        protected readonly DuplicationStatistics _duplicationStatistics;
        private readonly ILogger _logger;
        private readonly IApplicationDiagnostics _applicationDiagnostics;

        public Duplication(ILogger logger, IConfiguration configuration, IApplicationDiagnostics applicationDiagnostics)
        {
            _logger = logger;
            _configuration = configuration;
            _applicationDiagnostics = applicationDiagnostics;
            _duplicationStatistics = new DuplicationStatistics();
            _logger.LogDebug("Dupe Constructor Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        }

        /// <summary>
        /// Apply an MD5 Checksum to all rootEntries
        /// </summary> 
        /// <param name="rootEntries">Collection of rootEntries</param>
        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            _logger.LogDebug("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var newMatches = GetSizePairs(rootEntries);
            _logger.LogDebug("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

            var totalFilesInRootEntries = rootEntries.Sum(x => x.FileEntryCount);
            var totalEntriesInSizeDupes = newMatches.Sum(x => x.Value.Count);
            var longestListLength = newMatches.Count > 0 ? newMatches.Max(x => x.Value.Count) : -1;
            var longestListSize = newMatches.Count > 0
                                      ? newMatches.First(x => x.Value.Count == longestListLength).Key
                                      : 0;
            _logger.LogInfo("Found {0} sets of files matched by file size", newMatches.Count);
            _logger.LogInfo("Total files processed for the file size matches is {0}",
                            totalFilesInRootEntries);
            _logger.LogInfo("Total files found with at least 1 other file of same length {0}",
                            totalEntriesInSizeDupes);
            _logger.LogInfo("Longest list of same sized files is {0} for size {1} ", longestListLength,
                                          longestListSize);

            //flatten
            _logger.LogDebug("Flatten List..");
            var flatList = newMatches.SelectMany(dirlist => dirlist.Value).ToList();
            _logger.LogDebug("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            
            //group by volume/network share
            _logger.LogDebug("GroupBy Volume/Share..");

            // QUOTE 
            // The IGrouping<TKey, TElement> objects are yielded in an order based on 
            // the order of the elements in source that produced the first key of each 
            // IGrouping<TKey, TElement>. Elements in a grouping are yielded in the 
            // order they appear in source.
            //
            // by ordering from largest to smallest the larger files are hashed first
            // so a break of process and then running of dupes is a win for larger files.
            var descendingFlatList = flatList.OrderByDescending(
                pde => pde.ChildDE.IsDirectory ? 0 : pde.ChildDE.Size); // directories last

            var some = descendingFlatList.Take(20);
            foreach (var pairDirEntry in some)
            {
                Console.WriteLine("{0}", pairDirEntry.ChildDE.Size);
            }

            var groupedByDirectoryRoot = descendingFlatList
                .GroupBy(x => AlphaFSHelper.GetDirectoryRoot(x.FullPath));
            _logger.LogDebug("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

            //parrallel at the grouping level, hopefully this is one group per disk.
            _logger.LogDebug("Beginning Hashing...");
            _logger.LogDebug("Memory: {0}",_applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            
            var timer = new Stopwatch();
            timer.Start();

            var cts = new CancellationTokenSource();
            var token = cts.Token;
            var innerPhaseBreak = false;
            var outerOptions = new ParallelOptions {CancellationToken = token};

            try
            {
                Parallel.ForEach(groupedByDirectoryRoot, outerOptions, (grp, loopState) =>
                {
                    var parallelOptions = new ParallelOptions
                        {
                            CancellationToken = token,
                            MaxDegreeOfParallelism = 2
                        };
                    Parallel.ForEach(grp, parallelOptions,
                                     (flatFile, innerLoopState) => {
                                         CalculatePartialMD5Hash(flatFile.FullPath, flatFile.ChildDE);
                                         if (Hack.BreakConsoleFlag)
                                         {
                                             if (!innerPhaseBreak)
                                             {
                                                 innerPhaseBreak = true;
                                                 Console.WriteLine(
                                                     "\n * Break key detected exiting hashing phase inner.");
                                                 cts.Cancel();
                                             }
                                         }
                                     });
                });
            }
            catch (OperationCanceledException) {}
            catch (Exception ex)
            {
                //parallel cancellation. will be OperationCancelled or Aggregate Exception
                Console.WriteLine("Exception Type {0}", ex.GetType());
                Console.WriteLine(ex.Message);
                return;
            }

            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine("\n * Break key detected skipping full hashing phase.");
            }
            else
            {
                CheckDupesAndCompleteFullHash(rootEntries);
            }
            _logger.LogInfo(string.Empty);
            timer.Stop();
            var perf = string.Format("{0:F2} MB/s",
                    ((_duplicationStatistics.BytesProcessed*(1000.0/timer.ElapsedMilliseconds)))/
                    (1024.0*1024.0)
                );

            var statsMessage =
                string.Format(
                    "FullHash: {0}  PartialHash: {1}  Processed: {2:F2} MB Perf: {3}\nFailedHash: {4} (amost always because cannot open to read file)",
                    _duplicationStatistics.FullHashes, _duplicationStatistics.PartialHashes,
                    _duplicationStatistics.BytesProcessed/(1024*1024),
                    perf, _duplicationStatistics.FailedToHash);
            _logger.LogInfo(statsMessage);
        }

        public IDictionary<long, List<PairDirEntry>> GetSizePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseTreePair(rootEntries, FindMatchesOnFileSize2);
            _logger.LogDebug("Post TraverseMatchOnFileSize: {0}, dupeDictCount {1}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize.Count);

            //Remove the single values from the dictionary.  DOESNT SEEM TO CLEAR MEMORY ??? GC Force?
            _duplicateFileSize.Where(kvp => kvp.Value.Count == 1).ToList().ForEach(x => _duplicateFileSize.Remove(x.Key));
            _logger.LogDebug("Deleted entries from dictionary: {0}, dupeDictCount {1}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize.Count);
            return _duplicateFileSize;
        }

        private bool FindMatchesOnFileSize2(CommonEntry ce, DirEntry de)
        {
            if (de.IsDirectory || de.Size == 0) // || dirEntry.Size < 4096)
            {
                return true;
            }

            var flatDirEntry = new PairDirEntry(ce, de);
            if (_duplicateFileSize.ContainsKey(de.Size))
            {
                _duplicateFileSize[de.Size].Add(flatDirEntry);
            }
            else
            {
                _duplicateFileSize[de.Size] = new List<PairDirEntry> { flatDirEntry };
            }
            return true;
        }

        private void CalculatePartialMD5Hash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory || de.IsHashDone)
            {
                return;
            }
            CalculateMD5Hash(fullPath, de, true);
        }

        private void CheckDupesAndCompleteFullHash(IEnumerable<RootEntry> rootEntries)
        {
            _logger.LogDebug(string.Empty);
            _logger.LogDebug("Checking duplicates and completing full hash.");
            CommonEntry.TraverseTreePair(rootEntries, BuildDuplicateListIncludePartialHash);

            var founddupes = _duplicateFile.Where(d => d.Value.Count > 1);
            var totalEntriesInDupes = founddupes.Sum(x => x.Value.Count);
            var longestListLength = founddupes.Any() ? founddupes.Max(x => x.Value.Count) : 0;
            _logger.LogInfo("Found {0} duplication collections.", founddupes.Count());
            _logger.LogInfo("Total files found with at least 1 other file duplicate {0}",
                            totalEntriesInDupes);
            _logger.LogInfo("Longest list of duplicate files is {0}", longestListLength);

            foreach (var keyValuePair in founddupes)
            {
                foreach (var pairDirEntry in keyValuePair.Value)
                {
                    _dirEntriesRequiringFullHashing.Add(pairDirEntry.ChildDE);
                }
            }
            CommonEntry.TraverseTreePair(rootEntries, CalculateFullMD5Hash);
        }

        private void CalculateMD5Hash(string fullPath, DirEntry de, bool doPartialHash)
        {
            var displayCounterInterval = _configuration.ProgressUpdateInterval > 1000
                                             ? _configuration.ProgressUpdateInterval/10
                                             : _configuration.ProgressUpdateInterval;

            var configuration = new Configuration();
            if (doPartialHash)
            {
                //dont recalculate.
                if (de.IsHashDone && de.IsPartialHash)
                {
                    return;
                }
                var hashResponse = HashHelper.GetMD5HashResponseFromFile(fullPath, configuration.HashFirstPassSize);

                if (hashResponse != null)
                {
                    de.SetHash(hashResponse.Hash);
                    de.IsPartialHash = hashResponse.IsPartialHash;
                    _duplicationStatistics.BytesProcessed += hashResponse.BytesHashed;
                    if (de.IsPartialHash)
                    {
                        _duplicationStatistics.PartialHashes += 1;
                    }
                    else
                    {
                        _duplicationStatistics.FullHashes += 1;
                    }
                        
                    //_logger.LogDebug("Thread:{0}, File: {1}",Thread.CurrentThread.ManagedThreadId,fullPath);
                        
                    if (_duplicationStatistics.PartialHashes%displayCounterInterval == 0)
                    {
                        Console.Write("p");
                    }
                    if (_duplicationStatistics.FullHashes%displayCounterInterval == 0)
                    {
                        Console.Write("f");
                        Console.Write(" {0} ", hashResponse.BytesHashed);
                    }
                }
                else
                {
                    _duplicationStatistics.FailedToHash += 1;
                }
            }
            else
            {
                if (de.IsHashDone && !de.IsPartialHash)
                {
                    return;
                }
                var hashResponse = HashHelper.GetMD5HashFromFile(fullPath);
                if (hashResponse != null)
                {
                    de.SetHash(hashResponse.Hash);
                    de.IsPartialHash = hashResponse.IsPartialHash;
                    _duplicationStatistics.FullHashes += 1;
                    _duplicationStatistics.BytesProcessed += hashResponse.BytesHashed;
                    if (_duplicationStatistics.FullHashes%displayCounterInterval == 0)
                    {
                        Console.Write("f");
                    }
                }
                else
                {
                    _duplicationStatistics.FailedToHash += 1;
                }
            }
        }

        private bool CalculateFullMD5Hash(CommonEntry parentEntry, DirEntry dirEntry)
        {
            //ignore if we already have a hash.
            if (dirEntry.IsHashDone)
            {
                if (!dirEntry.IsPartialHash)
                {
                    return true;
                }

                if (_dirEntriesRequiringFullHashing.Contains(dirEntry))
                {
                    var fullPath = CommonEntry.MakeFullPath(parentEntry, dirEntry);
                    var longFullPath = Path.GetFullPath(fullPath, true);
                    CalculateMD5Hash(longFullPath, dirEntry, false);
                    if (Hack.BreakConsoleFlag)
                    {
                        Console.WriteLine("\n * Break key detected exiting full hashing phase outer.");
                        return false;
                    }
                }
            }
            return true;
        }

        private bool BuildDuplicateListIncludePartialHash(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (dirEntry.IsDirectory || !dirEntry.IsHashDone || dirEntry.Size == 0)
            {
                //TODO: how to deal with uncalculated files?
                return true;
            }

            var info = new PairDirEntry(parentEntry, dirEntry);
            if (_duplicateFile.ContainsKey(dirEntry))
            {
                _duplicateFile[dirEntry].Add(info);
            }
            else
            {
                _duplicateFile[dirEntry] = new List<PairDirEntry> { info };
            }
            return true;
        }

        private bool BuildDuplicateList(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (!dirEntry.IsPartialHash)
            {
                BuildDuplicateListIncludePartialHash(parentEntry, dirEntry);
            }
            return true;
        }

        public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
        {
            //TODO: What if we don't have md5 hash? go and create it? -- Rob votes not.
            var dupePairs = GetDupePairs(rootEntries)
                .OrderByDescending(kvp => kvp.Key.Size); // output larger duplicate files earlier
            foreach (var dupe in dupePairs)
            {
                _logger.LogInfo("--------------------------------------");
                dupe.Value.ForEach(v => Console.WriteLine("{0}", v.FullPath));
            }
        }

        public IList<KeyValuePair<DirEntry, List<PairDirEntry>>> GetDupePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseTreePair(rootEntries, BuildDuplicateList);
            var moreThanOneFile = _duplicateFile.Where(d => d.Value.Count > 1).ToList();

            _logger.LogInfo("Count of list of all hashes of files with same sizes {0}",
                                          _duplicateFile.Count);
            _logger.LogInfo("Count of list of all hashes of files with same sizes where more than 1 of that hash {0}",
                    moreThanOneFile.Count);
            return moreThanOneFile;
        }
    }
}