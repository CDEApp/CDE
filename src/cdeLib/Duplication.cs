using System;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public class Duplication
    {
        public void ApplyMd5Checksum(IEnumerable<RootEntry> rootEntries)
        {
            FindAllEntries(rootEntries);
            foreach(var rootEntry in rootEntries)
            {
                rootEntry.SaveRootEntry();
            }
        }

        private void FindAllEntries(IEnumerable<RootEntry> rootEntries)
        {
            foreach (var rootEntry in rootEntries)
            {
                foreach (var entry in rootEntry.Children)
                {
                    FindEntry(rootEntry.RootPath, entry);
                    Console.WriteLine(entry.Name);
                }
                
            }
        }

        private static void FindEntry(string path, DirEntry de)
        {
            foreach (var f in de.Children)
            {
                if (f.IsDirectory)
                {
                    var newPath = Path.Combine(path, de.Name);
                    newPath = Path.Combine(newPath, f.Name);
                    FindEntry(newPath, f);
                }
                else
                {
                    var fullPath = Path.Combine(path, f.Name);
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
            }
        }

        public void FindDuplicates(IEnumerable<RootEntry> rootEntries)
        {
            //TODO: What if we don't have md5 hash? go and create it? 
            
        }
    }
}