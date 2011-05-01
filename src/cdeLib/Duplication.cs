using System;
using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public class Duplication
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, List<string>> _dupes;
        private readonly Dictionary<string, int> _stats = new Dictionary<string, int>();
        private readonly Dictionary<ulong,List<FlatDirEntryDTO>> _duplicateFileSize = new Dictionary<ulong, List<FlatDirEntryDTO>>();
        private readonly Dictionary<string, List<string>> _duplicateFileList = new Dictionary<string, List<string>>();
        private readonly IConfiguration _configuration;
        
        public Duplication()
        {
            _logger = new Logger();
            _stats.Add("partial",0);
            _stats.Add("full", 0);
            _dupes = new Dictionary<string, List<string>>();
            _configuration = new Configuration();
        }

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTrees(rootEntries, FindMatchesOnFileSize);
            Console.WriteLine("Found {0} sets of files matched by filesize",_duplicateFileSize.Count);
            foreach(var kvp in _duplicateFileSize)
            {
                foreach(var flatFile in kvp.Value)
                {
                    CalculatePartialMD5Hash(flatFile.FilePath,flatFile.DirEntry);
                }
            }

            CheckDupesAndCompleteFullHash(rootEntries);
            foreach(var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
            Console.WriteLine("");
            Console.WriteLine("FullHash: {0}  PartialHash: {1}",_stats["full"],_stats["partial"]);
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
            _logger.LogInfo(String.Format("Found {0} dupes",founddupes.Count()));
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
            if (!String.IsNullOrEmpty(de.MD5Hash) && !de.IsPartialHash)
            {
                return;
            }
            if (de.MD5Hash != null && _dupes.ContainsKey(de.MD5Hash))
            {
                CalculateMD5Hash(fullPath, de, false);
            }
        }

        private void CalculatePartialMD5Hash(string fullPath, DirEntry de)
        {
            if (de.IsDirectory)
                return;
            
            //ignore if we already have a hash.
            if (!String.IsNullOrEmpty(de.MD5Hash))
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
                string hash;
                if (doPartialHash)
                {
                    //dont recalculate.
                    if (!String.IsNullOrEmpty(de.MD5Hash) && de.IsPartialHash)
                    {
                        return;
                    }
                    hash = hashHelper.GetMD5HashFromFile(fullPath, configuration.HashFirstPassSize);
                    de.MD5Hash = hash;
                    de.IsPartialHash = true;
                    _stats["partial"] += 1;
                    if (_stats["partial"] % displayCounterInterval == 0)
                    {
                        Console.Write("p");
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(de.MD5Hash) && !de.IsPartialHash)
                    {
                        return;
                    }
                    hash = hashHelper.GetMD5HashFromFile(fullPath);
                    de.MD5Hash = hash;
                    de.IsPartialHash = false;
                    _stats["full"] += 1;
                    if (_stats["full"] % displayCounterInterval == 0)
                    {
                        Console.Write("f");
                    }
                }
            }
        }


        private void BuildDuplicateList(string fullPath, DirEntry de)
        {
            if (String.IsNullOrEmpty(de.MD5Hash))
            {
                //TODO: how to deal with uncalculated files?
                return;
            }
            //add duplicate
            if (_duplicateFileList.ContainsKey(de.MD5Hash))
            {
                _duplicateFileList[de.MD5Hash].Add(fullPath);
            }
            else
            {
                //create new entry.
                _duplicateFileList.Add(de.MD5Hash,new List<string> {fullPath});
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