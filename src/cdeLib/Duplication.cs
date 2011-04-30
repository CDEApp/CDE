using System;
using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public class FlatFile
    {
        public FlatFile(string filePath, DirEntry dirEntry)
        {
            FilePath = filePath;
            DirEntry = dirEntry;
        }

        public string FilePath { get; set; }
        public DirEntry DirEntry { get; set; }
    }

    public class Duplication
    {
        private readonly ILogger _logger;
        private Dictionary<string, List<string>> dupes;
        private Dictionary<string, int> _stats = new Dictionary<string, int>();
        private Dictionary<ulong,List<FlatFile>> _duplicateFileSize = new Dictionary<ulong, List<FlatFile>>();
        public Duplication()
        {
            _logger = new Logger();
            _stats.Add("partial",0);
            _stats.Add("full", 0);
            dupes = new Dictionary<string, List<string>>();
        }

        private Dictionary<string, List<string>> _duplicateFileList = new Dictionary<string, List<string>>();

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTrees(rootEntries, FindMatchesOnFileSize);
            Console.WriteLine("Found {0} sets of files matched by filesize",_duplicateFileSize.Count);
            Logger l = new Logger();
            foreach(var kvp in _duplicateFileSize)
            {
                foreach(var flatFile in kvp.Value)
                {
                    CalculatePartialMD5Hash(flatFile.FilePath,flatFile.DirEntry);
                }
            }
//            foreach (var kvp in _duplicateFileSize)
//            {
//                foreach (var flatFile in kvp.Value)
//                {
//                    CalculatePartialMD5Hash(flatFile.FilePath, flatFile.DirEntry);
//                }
//            }

            //CommonEntry.TraverseAllTrees(rootEntries, CalculatePartialMD5Hash);

            CheckDupesAndCompleteFullHash(rootEntries);
            foreach(var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
            Console.WriteLine("FullHash: {0}  PartialHash: {1}",_stats["full"],_stats["partial"]);
        }

        private void FindMatchesOnFileSize(string filePath, DirEntry dirEntry)
        {
            if (_duplicateFileSize.ContainsKey(dirEntry.Size))
            {
               _duplicateFileSize[dirEntry.Size].Add(new FlatFile(filePath,dirEntry));
            }
            else
            {
                _duplicateFileSize.Add(dirEntry.Size, new List<FlatFile>() { new FlatFile(filePath, dirEntry) }); 
            }
        }

        private void CheckDupesAndCompleteFullHash(IEnumerable<RootEntry> rootEntries)
        {
            Console.WriteLine("CheckDupesAndCOmpleteFullHash");
            CommonEntry.TraverseAllTrees(rootEntries, BuildDuplicateList);
            var founddupes = _duplicateFileList.Where(d => d.Value.Count > 1);
            Console.WriteLine("Found {0} dupes",founddupes.Count());
            foreach (var keyValuePair in founddupes)
            {
                dupes.Add(keyValuePair.Key,keyValuePair.Value);
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
            if (de.MD5Hash != null && dupes.ContainsKey(de.MD5Hash))
            {
                //_logger.LogInfo(String.Format("Calculating full hash for {0}", fullPath));
                CalculateMD5Hash(fullPath, de, false);
            }
            else
            {
                //_logger.LogInfo(String.Format("NOT Calculating full hash for {0}", fullPath));
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
            if (File.Exists(fullPath))
            {
                var hashHelper = new HashHelper();
                var configuration = new Configuration();
                string hash = String.Empty;
                if (doPartialHash)
                {
                    //dont recalculate.
                    if (!String.IsNullOrEmpty(de.MD5Hash) && de.IsPartialHash)
                    {
                        return;
                    }
                    hash = hashHelper.GetMD5HashFromFile(fullPath, configuration.HashFirstPassSize);
                    //TODO: Need to convert method to use stream, currently getting out of memory for large files.
                    //var hash = HashHelper.GetMurmerHashFromFile(fullPath)
                    de.MD5Hash = hash;
                    de.IsPartialHash = true;
                    _stats["partial"] += 1;
                    if (_stats["partial"]%10 == 0)
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
                    //TODO: Need to convert method to use stream, currently getting out of memory for large files.
                    //var hash = HashHelper.GetMurmerHashFromFile(fullPath)
                    de.MD5Hash = hash;
                    de.IsPartialHash = false;
                    _stats["full"] += 1;
                    if (_stats["full"]%10 == 0)
                    {
                        Console.Write("f");
                    }
                }
                //_logger.LogInfo(String.Format("{0} {1}", fullPath, hash));    
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
            var dupes = _duplicateFileList.Where(d=>d.Value.Count>1).ToList();
            foreach(var dupe in dupes)
            {
                Console.WriteLine("--------------------------------------");
                dupe.Value.ForEach(v=>Console.WriteLine("{0}",v));
            }

        }
    }
}