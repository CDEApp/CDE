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
        public Duplication()
        {
            _logger = new Logger();
        }

        private Dictionary<string, List<string>> _duplicateFileList = new Dictionary<string, List<string>>();

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            FindAllEntries(rootEntries, CalculateMD5Hash);
            foreach(var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
        }

        private void FindAllEntries(IEnumerable<RootEntry> rootEntries, Action<string, DirEntry> action)
        {
            foreach (var rootEntry in rootEntries)
            {
                foreach (var entry in rootEntry.Children)
                {
                    FindEntry(rootEntry.RootPath, entry, action);
                }
                
            }
        }

        private static void FindEntry(string path, DirEntry de, Action<string,DirEntry> action)
        {
            foreach (var f in de.Children)
            {
                if (f.IsDirectory)
                {
                    var newPath = Path.Combine(path, de.Name);
                    newPath = Path.Combine(newPath, f.Name);
                    FindEntry(newPath, f,action);
                }
                else
                {
                    var fullPath = Path.Combine(path, f.Name);
                    action(fullPath, f);
                }
            }
        }

        private void CalculateMD5Hash(string fullPath, DirEntry de)
        {
            if (File.Exists(fullPath))
            {
                if (!String.IsNullOrEmpty(de.MD5Hash))
                {
                    _logger.LogInfo(String.Format("{0} already has md5 {1} ", fullPath, de.MD5Hash));
                }
                else
                {
                    var hash = HashHelper.GetMD5HashFromFile(fullPath);
                    //TODO: Need to convert method to use stream, currently getting out of memory for large files.
                    //var hash = HashHelper.GetMurmerHashFromFile(fullPath)
                    de.MD5Hash = hash;
                    _logger.LogInfo(String.Format("{0} {1}", fullPath, hash));    
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
            FindAllEntries(rootEntries, BuildDuplicateList);
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