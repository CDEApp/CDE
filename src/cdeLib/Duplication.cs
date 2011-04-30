using System;
using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public class Duplication
    {
        private Dictionary<string, List<string>> _duplicateFileList = new Dictionary<string, List<string>>();

        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            CommonEntry.TraverseAllTrees(rootEntries, CalculateMD5Hash);

            foreach(var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
        }

        private void CalculateMD5Hash(string fullPath, DirEntry de)
        {
            if (File.Exists(fullPath))
            {
                if (!String.IsNullOrEmpty(de.MD5Hash))
                {
                    Console.WriteLine("{0} already has md5 {1} ", fullPath, de.MD5Hash);
                }
                else
                {
                    var hash = HashHelper.GetMD5HashFromFile(fullPath);
                    de.MD5Hash = hash;
                    Console.WriteLine("{0} {1}", fullPath, hash);    
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
            var dupes = _duplicateFileList.Where(d=>d.Value.Count>1).ToList();
            foreach(var dupe in dupes)
            {
                Console.WriteLine("--------------------------------------");
                dupe.Value.ForEach(v=>Console.WriteLine("{0}",v));
            }

        }
    }
}