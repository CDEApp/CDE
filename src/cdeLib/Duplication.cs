using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Comparer;

namespace cdeLib
{
    public class DuplicationStatistics
    {
        public DuplicationStatistics()
        {
            PartialHashes = 0;
            FullHashes = 0;
            BytesProcessed = 0;

        }

        public long PartialHashes { get; set; }
        public long FullHashes { get; set; }
        public long BytesProcessed { get; set; }
    }

    public class Duplication
    {
        private readonly ILogger _logger;
        private readonly Dictionary<byte[], List<string>> _dupes;
        private Dictionary<ulong,List<FlatDirEntryDTO>> _duplicateFileSize = new Dictionary<ulong, List<FlatDirEntryDTO>>();
        private readonly Dictionary<byte[], List<string>> _duplicateFileList = new Dictionary<byte[], List<string>>(new ByteArrayComparer());
        private readonly IConfiguration _configuration;
        private readonly DuplicationStatistics _duplicationStatistics;
        
        public Duplication()
        {
            _logger = new Logger();
            _dupes = new Dictionary<byte[], List<string>>(new ByteArrayComparer());
            _configuration = new Configuration();
            _duplicationStatistics = new DuplicationStatistics();
        }

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            var timer = new Stopwatch();
            timer.Start();
            
            CommonEntry.TraverseAllTrees(rootEntries, FindMatchesOnFileSize);

            // prune all entries that only have one file of that size
            var newMatches = _duplicateFileSize.Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            _duplicateFileSize = newMatches;

            Console.WriteLine("Found {0} sets of files matched by filesize",_duplicateFileSize.Count);
            foreach(var kvp in _duplicateFileSize)
            {
                foreach(var flatFile in kvp.Value)
                {
                    CalculatePartialMD5Hash(flatFile.FilePath,flatFile.DirEntry);
                }
            }
            CheckDupesAndCompleteFullHash(rootEntries);
            Console.WriteLine("");
            timer.Stop();
            string perf = String.Format("{0:F2} MB/s",
                ((_duplicationStatistics.BytesProcessed * (1000.0 / timer.ElapsedMilliseconds))) / (1024.0 * 1024.0)
                );

            Console.WriteLine("FullHash: {0}  PartialHash: {1}  Processed: {2:F2} MB Perf: {3}", _duplicationStatistics.FullHashes, _duplicationStatistics.PartialHashes,
                _duplicationStatistics.BytesProcessed/ (1024*1024),
                perf);
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }

        }

        private void FindMatchesOnFileSize(string filePath, DirEntry dirEntry)
        {
            if (_duplicateFileSize.ContainsKey(dirEntry.Size))
            {
               _duplicateFileSize[dirEntry.Size].Add(new FlatDirEntryDTO(filePath,dirEntry));
            }
            else
            {
                _duplicateFileSize.Add(dirEntry.Size, new List<FlatDirEntryDTO> { new FlatDirEntryDTO(filePath, dirEntry) }); 
            }
        }

        private void CheckDupesAndCompleteFullHash(IEnumerable<RootEntry> rootEntries)
        {
            _logger.LogDebug("");
            _logger.LogDebug("CheckDupesAndCompleteFullHash");
            CommonEntry.TraverseAllTrees(rootEntries, BuildDuplicateList);
            var founddupes = _duplicateFileList.Where(d => d.Value.Count > 1);
            _logger.LogInfo(String.Format("Found {0} duplication collections.",founddupes.Count()));
            foreach (var keyValuePair in founddupes)
            {
                _dupes.Add(keyValuePair.Key,keyValuePair.Value);
            }
            CommonEntry.TraverseAllTrees(rootEntries, CalculateFullMD5Hash);
        }

        private void CalculateFullMD5Hash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory)
                return;

            //ignore if we already have a hash.
            if (de.Hash != null && !de.IsPartialHash)
            {
                return;
            }
            if (de.Hash != null && _dupes.ContainsKey(de.Hash))
            {
                CalculateMD5Hash(fullPath, de, false);
            }
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
                            _duplicationStatistics.PartialHashes += 1;
                        else
                            _duplicationStatistics.FullHashes += 1;
                        if (_duplicationStatistics.PartialHashes % displayCounterInterval == 0)
                        {
                            Console.Write("p");
                        }
                        if (_duplicationStatistics.FullHashes % displayCounterInterval == 0)
                        {
                            Console.Write("f");
                        }
                    }
                }
                else
                {
                    if ((de.Hash != null) && !de.IsPartialHash)
                    {
                        return;
                    }
                    var hashResponse = hashHelper.GetMD5HashFromFile(fullPath);
                    de.Hash = hashResponse.Hash;
                    de.IsPartialHash = hashResponse.IsPartialHash;
                    _duplicationStatistics.FullHashes += 1;
                    _duplicationStatistics.BytesProcessed += hashResponse.BytesHashed;
                    if (_duplicationStatistics.FullHashes % displayCounterInterval == 0)
                    {
                        Console.Write("f");
                    }
                }
            }
        }


        private void BuildDuplicateList(string fullPath, DirEntry de)
        {
            if (de.Hash == null)
            {
                //TODO: how to deal with uncalculated files?
                return;
            }
            //add duplicate
            if (_duplicateFileList.ContainsKey(de.Hash))
            {
                _duplicateFileList[de.Hash].Add(fullPath);
            }
            else
            {
                //create new entry.
                _duplicateFileList.Add(de.Hash,new List<string> {fullPath});
            }
        }

        public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
        {
            //TODO: What if we don't have md5 hash? go and create it? 
            CommonEntry.TraverseAllTrees(rootEntries, BuildDuplicateList);
            //Display
            var dupePairs = _duplicateFileList.Where(d=>d.Value.Count>1).ToList();
            foreach(var dupe in dupePairs)
            {
                _logger.LogInfo("--------------------------------------");
                dupe.Value.ForEach(v=>Console.WriteLine("{0}",v));
            }

        }
    }
}