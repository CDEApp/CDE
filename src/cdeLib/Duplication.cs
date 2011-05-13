using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Comparer;
using cdeLib.Infrastructure.Hashing;

namespace cdeLib
{


    public class Duplication
    {
        

        private readonly IConfiguration _configuration;

        private readonly Dictionary<byte[], List<PairDirEntry>> _duplicateFile =
            new Dictionary<byte[], List<PairDirEntry>>(new ByteArrayComparer());

        private readonly Dictionary<ulong, List<PairDirEntry>> _duplicateFileSize2 =
            new Dictionary<ulong, List<PairDirEntry>>();

        private readonly Dictionary<byte[], List<PairDirEntry>> _duplicateForFullHash =
            new Dictionary<byte[], List<PairDirEntry>>(new ByteArrayComparer());

        private readonly DuplicationStatistics _duplicationStatistics;
        private readonly ILogger _logger;
        private readonly IApplicationDiagnostics _applicationDiagnostics;

        public Duplication(ILogger logger, IConfiguration configuration, IApplicationDiagnostics applicationDiagnostics)
        {
            _logger = logger;
            _configuration = configuration;
            _applicationDiagnostics = applicationDiagnostics;
            _duplicationStatistics = new DuplicationStatistics();
            _logger.LogDebug(String.Format("Dupe Constructor Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));
        }

        /// <summary>
        /// Apply an MD5 Checksum to all rootEntries
        /// </summary> 
        /// <param name="rootEntries">Collection of rootEntries</param>
        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            _logger.LogDebug(String.Format("PrePairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));
            var newMatches = GetSizePairs(rootEntries);
            _logger.LogDebug(String.Format("PostPairSize Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));

            var totalFilesInRootEntries = rootEntries.Sum(x => x.FileCount);
            var totalEntriesInSizeDupes = newMatches.Sum(x => x.Value.Count);
            var longestListLength = newMatches.Count > 0 ? newMatches.Max(x => x.Value.Count) : -1;
            var longestListSize = newMatches.Count > 0
                                      ? newMatches.First(x => x.Value.Count == longestListLength).Key
                                      : 0;
            _logger.LogInfo(string.Format("Found {0} sets of files matched by file size", newMatches.Count));
            _logger.LogInfo(string.Format("Total files processed for the file size matches is {0}",
                                          totalFilesInRootEntries));
            _logger.LogInfo(string.Format("Total files found with at least 1 other file of same length {0}",
                                          totalEntriesInSizeDupes));
            _logger.LogInfo(string.Format("Longest list of same sized files is {0} for size {1} ", longestListLength,
                                          longestListSize));

            //flatten
            _logger.LogDebug("Flatten List..");
            var flatList = newMatches.SelectMany(dirlist => dirlist.Value).ToList();
            _logger.LogDebug(String.Format("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));

            //group by volume/network share
            _logger.LogDebug("GroupBy Volume/Share..");
            var groupedByDirectoryRoot = flatList.GroupBy(x => FileSystemHelper.GetDirectoryRoot(x.FilePath));
            _logger.LogDebug(String.Format("Memory: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));

            //parralel at the grouping level, hopefully this is one group per disk.
            _logger.LogDebug("Beging Hashing...");
            _logger.LogDebug(String.Format("Memory: {0}",_applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()));
            
            var timer = new Stopwatch();
            timer.Start();

            var cts = new CancellationTokenSource();
            var token = cts.Token;
            var outerOptions = new ParallelOptions {CancellationToken = token};
            try
            {
              
                Parallel.ForEach(groupedByDirectoryRoot, outerOptions, (grp, loopState) =>
                                                                           {
                                                                               var parallelOptions = new ParallelOptions();
                                                                               parallelOptions.MaxDegreeOfParallelism = _configuration.ProgressUpdateInterval;
                                                                               parallelOptions.CancellationToken = token;
                                                                               Parallel.ForEach(grp, parallelOptions,
                                                                                                (flatFile, innerLoopState) =>
                                                                                                    {
                                                                                                        CalculatePartialMD5Hash
                                                                                                            (flatFile.FilePath,
                                                                                                             flatFile.ChildDE);
                                                                                                        if (Hack.BreakConsoleFlag)
                                                                                                        {
                                                                                                            //Console.WriteLine("\nBreak key detected exiting hashing phase inner.");
                                                                                                            cts.Cancel();
                                                                                                            parallelOptions.CancellationToken.ThrowIfCancellationRequested();
                                                                                                        }

                                                                                                        if (Hack.BreakConsoleFlag)
                                                                                                        {
                                                                                                            //Console.WriteLine ("\nBreak key detected exiting hashing phase outer.");
                                                                                                            cts.Cancel();
                                                                                                        }
                                                                                                    });
                                                                           });
            }
            catch (Exception ex)
            {
                //parallel cancellation. will be OperationCancelled or Aggregate Exception
                Console.WriteLine(ex.Message);
            }

            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine("\nBreak key detected skipping full hashing phase.");
            }
            else
            {
                CheckDupesAndCompleteFullHash(rootEntries);
            }
            _logger.LogInfo("");
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
            _logger.LogInfo(string.Format(statsMessage));

            foreach (var rootEntry in rootEntries)
            {
                _logger.LogDebug(String.Format("Saving {0}", rootEntry.DefaultFileName));
                rootEntry.SaveRootEntry();
            }
        }

        public IDictionary<ulong, List<PairDirEntry>> GetSizePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTreesPair(rootEntries, FindMatchesOnFileSize2);
            _logger.LogDebug(String.Format("Post TraverseMatchOnFileSize: {0}, dupeDictCount {1}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize2.Count));

            //Remove the single values from the dictionary.  DOESNT SEEM TO CLEAR MEMORY ??? GC Force?
            _duplicateFileSize2.Where(kvp => kvp.Value.Count == 1).ToList().ForEach(x => _duplicateFileSize2.Remove(x.Key));
            _logger.LogDebug(String.Format("Deleted entries from dictionary: {0}, dupeDictCount {1}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes(), _duplicateFileSize2.Count));
            return _duplicateFileSize2;
        }

        private void CalculatePartialMD5Hash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory)
                return;

            //ignore if we already have a hash.
            if (de.Hash != null)
            {
                return;
            }
            CalculateMD5Hash(fullPath, de, true);
        }

        private void CheckDupesAndCompleteFullHash(IEnumerable<RootEntry> rootEntries)
        {
            _logger.LogDebug("");
            _logger.LogDebug("Checking duplicates and completing full hash.");
            CommonEntry.TraverseAllTreesPair(rootEntries, BuildDuplicateListIncludePartialHash);

            var founddupes = _duplicateFile.Where(d => d.Value.Count > 1);
            var totalEntriesInDupes = founddupes.Sum(x => x.Value.Count);
            var longestListLength = founddupes.Any() ? founddupes.Max(x => x.Value.Count) : 0;
            _logger.LogInfo(string.Format("Found {0} duplication collections.", founddupes.Count()));
            _logger.LogInfo(string.Format("Total files found with at least 1 other file duplicate {0}",
                                          totalEntriesInDupes));
            _logger.LogInfo(string.Format("Longest list of duplicate files is {0}", longestListLength));

            foreach (var keyValuePair in founddupes)
            {
                _duplicateForFullHash.Add(keyValuePair.Key, keyValuePair.Value);
                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine("\nBreak key detected exiting full hashing phase outer.");
                    break;
                }
            }
            CommonEntry.TraverseAllTreesPair(rootEntries, CalculateFullMD5Hash);
        }

        private void FindMatchesOnFileSize2(CommonEntry ce, DirEntry de)
        {
            if (de.IsDirectory || de.Size == 0) // || dirEntry.Size < 4096)
            {
                return;
            }

            var flatDirEntry = new PairDirEntry(ce, de);
            if (_duplicateFileSize2.ContainsKey(de.Size))
            {
                _duplicateFileSize2[de.Size].Add(flatDirEntry);
            }
            else
            {
                _duplicateFileSize2[de.Size] = new List<PairDirEntry> { flatDirEntry };
            }
        }

        private void CalculateMD5Hash(string fullPath, DirEntry de, bool doPartialHash)
        {
            var displayCounterInterval = _configuration.ProgressUpdateInterval > 1000
                                             ? _configuration.ProgressUpdateInterval/10
                                             : _configuration.ProgressUpdateInterval;
            if (File.Exists(fullPath))
            {
                var hashHelper = new HashHelper();
                var configuration = new Configuration();
                if (doPartialHash)
                {
                    //dont recalculate.
                    if (de.Hash != null && de.IsPartialHash)
                    {
                        return;
                    }
                    var hashResponse = hashHelper.GetMD5HashResponseFromFile(fullPath, configuration.HashFirstPassSize);

                    if (hashResponse != null)
                    {
                        de.Hash = hashResponse.Hash;
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
                        
                        //_logger.LogDebug(String.Format("Thread:{0}, File: {1}",Thread.CurrentThread.ManagedThreadId,fullPath));
                        
                        if (_duplicationStatistics.PartialHashes%displayCounterInterval == 0)
                        {
                            Console.Write("p");
                        }
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
                else
                {
                    if ((de.Hash != null) && !de.IsPartialHash)
                    {
                        return;
                    }
                    var hashResponse = hashHelper.GetMD5HashFromFile(fullPath);
                    if (hashResponse != null)
                    {
                        de.Hash = hashResponse.Hash;
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
        }

        private void CalculateFullMD5Hash(CommonEntry parentEntry, DirEntry dirEntry)
        {
            var a = parentEntry.FullPath ?? "pnull";
            var b = dirEntry.Name ?? "dnull";
            var fullPath = Path.Combine(a, b); if (dirEntry.IsDirectory)
                return;

            //ignore if we already have a hash.
            if (dirEntry.Hash != null)
            {
                if (!dirEntry.IsPartialHash)
                {
                    return;
                }

                if (_duplicateForFullHash.ContainsKey(dirEntry.KeyHash))
                {
                    CalculateMD5Hash(fullPath, dirEntry, false);
                }
            }
        }

        private void BuildDuplicateListIncludePartialHash(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (dirEntry.IsDirectory || dirEntry.Hash == null || dirEntry.Size == 0)
            {
                //TODO: how to deal with uncalculated files?
                return;
            }

            var info = new PairDirEntry(parentEntry, dirEntry);
            if (_duplicateFile.ContainsKey(dirEntry.KeyHash))
            {
                _duplicateFile[dirEntry.KeyHash].Add(info);
            }
            else
            {
                _duplicateFile[dirEntry.KeyHash] = new List<PairDirEntry> { info };
            }
        }

        private void BuildDuplicateList(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (!dirEntry.IsPartialHash)
            {
                BuildDuplicateListIncludePartialHash(parentEntry, dirEntry);
            }
        }

        public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
        {
            //TODO: What if we don't have md5 hash? go and create it? -- Rob votes not.
            var dupePairs = GetDupePairs(rootEntries);
            foreach (var dupe in dupePairs)
            {
                _logger.LogInfo("--------------------------------------");
                dupe.Value.ForEach(v => Console.WriteLine("{0}", v.FilePath));
            }
        }

        public IList<KeyValuePair<byte[], List<PairDirEntry>>> GetDupePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTreesPair(rootEntries, BuildDuplicateList);
            var moreThanOneFile = _duplicateFile.Where(d => d.Value.Count > 1).ToList();

            _logger.LogInfo(string.Format("Count of list of all hashes of files with same sizes {0}",
                                          _duplicateFile.Count));
            _logger.LogInfo(
                string.Format(
                    "Count of list of all hashes of files with same sizes where more than 1 of that hash {0}",
                    moreThanOneFile.Count));
            return moreThanOneFile;
        }
    }
}