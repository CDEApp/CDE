using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Comparer;
using cdeLib.Infrastructure.Hashing;

namespace cdeLib
{
    public class DuplicationStatistics
    {
        public DuplicationStatistics()
        {
            PartialHashes = 0;
            FullHashes = 0;
            BytesProcessed = 0;
            FailedToHash = 0;
        }

        public long PartialHashes { get; set; }
        public long FullHashes { get; set; }
        public long BytesProcessed { get; set; }
        public long FailedToHash { get; set; }
    }

    public class Duplication
    {
        private readonly ILogger _logger;
        private readonly Dictionary<ulong, List<FlatDirEntryDTO>> _duplicateFileSize = new Dictionary<ulong, List<FlatDirEntryDTO>>();
        private readonly Dictionary<byte[], List<FlatDirEntryDTO>> _duplicateFile = new Dictionary<byte[], List<FlatDirEntryDTO>>(new ByteArrayComparer());
        private readonly Dictionary<byte[], List<FlatDirEntryDTO>> _duplicateForFullHash = new Dictionary<byte[], List<FlatDirEntryDTO>>(new ByteArrayComparer());
        
        private readonly IConfiguration _configuration;
        private readonly DuplicationStatistics _duplicationStatistics;
        
        public Duplication()
        {
            _logger = new Logger();
            _configuration = new Configuration();
            _duplicationStatistics = new DuplicationStatistics();
        }

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            var timer = new Stopwatch();
            timer.Start();
            var newMatches = GetSizePairs(rootEntries);

            var totalFilesInRootEntries = rootEntries.Sum(x => x.FileCount);
            var totalEntriesInSizeDupes = newMatches.Sum(x => x.Value.Count);
            var longestListLength = newMatches.Count > 0 ? newMatches.Max(x => x.Value.Count) : -1;
            var longestListSize = newMatches.Count > 0 ? newMatches.First(x => x.Value.Count == longestListLength).Key : 0;
            _logger.LogInfo(string.Format("Found {0} sets of files matched by file size", newMatches.Count));
            _logger.LogInfo(string.Format("Total files processed for the file size matches is {0}", totalFilesInRootEntries));
            _logger.LogInfo(string.Format("Total files found with at least 1 other file of same length {0}", totalEntriesInSizeDupes));
            _logger.LogInfo(string.Format("Longest list of same sized files is {0} for size {1} ", longestListLength, longestListSize));

            foreach (var kvp in newMatches)
            {
                foreach (var flatFile in kvp.Value)
                {
                    CalculatePartialMD5Hash(flatFile.FilePath, flatFile.DirEntry);
                    if (Hack.BreakConsoleFlag)
                    {
                        Console.WriteLine("\nBreak key detected exiting hashing phase inner.");
                        break;
                    }
                }
                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine("\nBreak key detected exiting hashing phase outer.");
                    break;
                }
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
                ((_duplicationStatistics.BytesProcessed * (1000.0 / timer.ElapsedMilliseconds))) / (1024.0 * 1024.0)
                );

            var statsMessage = string.Format("FullHash: {0}  PartialHash: {1}  Processed: {2:F2} MB Perf: {3}\nFailedHash: {4} (amost always because cannot open to read file)", 
                _duplicationStatistics.FullHashes, _duplicationStatistics.PartialHashes,
                _duplicationStatistics.BytesProcessed/ (1024*1024),
                perf, _duplicationStatistics.FailedToHash);
            _logger.LogInfo(string.Format(statsMessage));

            foreach (var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
        }
        
        public IDictionary<ulong, List<FlatDirEntryDTO>> GetSizePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTrees(rootEntries, FindMatchesOnFileSize);
            return _duplicateFileSize.Where(kvp => kvp.Value.Count > 1)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
            CommonEntry.TraverseAllTrees(rootEntries, BuildDuplicateListIncludePartialHash);

            var founddupes = _duplicateFile.Where(d => d.Value.Count > 1);
            var totalEntriesInDupes = founddupes.Sum(x => x.Value.Count);
            var longestListLength = founddupes.Any() ? founddupes.Max(x => x.Value.Count) : 0;
            _logger.LogInfo(string.Format("Found {0} duplication collections.", founddupes.Count()));
            _logger.LogInfo(string.Format("Total files found with at least 1 other file duplicate {0}", totalEntriesInDupes));
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
            CommonEntry.TraverseAllTrees(rootEntries, CalculateFullMD5Hash);
        }

        private void FindMatchesOnFileSize(string filePath, DirEntry de)
        {
            if (de.IsDirectory || de.Size == 0) // || de.Size < 4096)
            {
                return;
            }

            var flatDirEntry = new FlatDirEntryDTO(filePath, de);  
            if (_duplicateFileSize.ContainsKey(de.Size))
            {
                _duplicateFileSize[de.Size].Add(flatDirEntry);
            }
            else
            {
                _duplicateFileSize[de.Size] = new List<FlatDirEntryDTO> { flatDirEntry };
            }
        }

        private void CalculateMD5Hash(string fullPath, DirEntry de, bool doPartialHash)
        {
            var displayCounterInterval = _configuration.ProgressUpdateInterval > 1000 ? _configuration.ProgressUpdateInterval / 10 : _configuration.ProgressUpdateInterval;
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
                        if (_duplicationStatistics.PartialHashes % displayCounterInterval == 0)
                        {
                            Console.Write("p");
                        }
                        if (_duplicationStatistics.FullHashes % displayCounterInterval == 0)
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
                        if (_duplicationStatistics.FullHashes % displayCounterInterval == 0)
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

        private void CalculateFullMD5Hash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory)
                return;

            //ignore if we already have a hash.
            if (de.Hash != null)
            {
                if (!de.IsPartialHash)
                {
                    return;
                }

                if (_duplicateForFullHash.ContainsKey(de.KeyHash))
                {
                    CalculateMD5Hash(fullPath, de, false);
                }
            }
        }

        private void BuildDuplicateListIncludePartialHash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory || de.Hash == null || de.Size == 0)
            {   //TODO: how to deal with uncalculated files?
                return;
            }

            var info = new FlatDirEntryDTO(fullPath, de);
            if (_duplicateFile.ContainsKey(de.KeyHash))
            {
                _duplicateFile[de.KeyHash].Add(info);
            }
            else
            {
                _duplicateFile[de.KeyHash] = new List<FlatDirEntryDTO> { info };
            }
        }

        private void BuildDuplicateList(string fullPath, DirEntry de)
        {
            if (!de.IsPartialHash)
            {
                BuildDuplicateListIncludePartialHash(fullPath, de);
            }
        }

        public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
        {
            //TODO: What if we don't have md5 hash? go and create it? -- Rob votes not.
            var dupePairs = GetDupePairs(rootEntries);
            foreach(var dupe in dupePairs)
            {
                _logger.LogInfo("--------------------------------------");
                dupe.Value.ForEach(v=>Console.WriteLine("{0}",v.FilePath));
            }
        }

        public IList<KeyValuePair<byte[], List<FlatDirEntryDTO>>> GetDupePairs(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTrees(rootEntries, BuildDuplicateList);
            var moreThanOneFile = _duplicateFile.Where(d => d.Value.Count > 1).ToList();

            _logger.LogInfo(string.Format("Count of list of all hashes of files with same sizes {0}", _duplicateFile.Count));
            _logger.LogInfo(string.Format("Count of list of all hashes of files with same sizes where more than 1 of that hash {0}", moreThanOneFile.Count));
            return moreThanOneFile;
        }
    }
}