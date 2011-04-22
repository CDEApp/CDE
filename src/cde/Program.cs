using System;
using System.IO;
using cdeLib;

namespace cde
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = @"C:\";
            CreateCDECache(path, @"D:\Test1.cde");
        }

        static void CreateCDECache(string path, string cacheFile)
        {
            var re = new RootEntry();
            re.RecurseTree(path);
            re.SetSummaryFields();

            var newFS = new FileStream(cacheFile, FileMode.Create);
            re.Write(newFS);
            newFS.Close();

            Console.WriteLine(string.Format("files {0} dirs {1} totalsize {2}",
                re.FileCount, re.DirCount, re.Size));
        }
    }
}


// IDEA test a list of fullpaths.