using System;
using System.IO;
using cdeLib;

namespace cde
{
    class Program
    {
        static string path = @"C:\";
        static string file = @"c_SM15T_1_3.cde";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                CreateCDECache(path, file);
            }
            else
            {
                Console.WriteLine("Finding " + args[0]);
                FindString(file, args[0]);
            }
        }

        static void FindString(string cacheFile, string find)
        {
            DateTime start = DateTime.UtcNow;
            var newFS = new FileStream(cacheFile, FileMode.Open);
            RootEntry rootEntry = RootEntry.Read(newFS);
            DateTime end = DateTime.UtcNow;
            var loadTimeSpan = end - start;
            Console.WriteLine("Loaded Done... {0} in {1}msecs", cacheFile, loadTimeSpan.Milliseconds);
            Console.WriteLine("Loaded Entries... {0}", rootEntry.DirCount + rootEntry.FileCount);

            rootEntry.FindEntries(find);
            
            Console.WriteLine("Done. ");

        }

        static void CreateCDECache(string path, string cacheFile)
        {
            var re = new RootEntry();
            re.RecurseTree(path);
            re.SetSummaryFields();
            re.RootPath = path;

            var newFS = new FileStream(cacheFile, FileMode.Create);
            re.Write(newFS);
            newFS.Close();

            Console.WriteLine(string.Format("files {0} dirs {1} totalsize {2}",
                re.FileCount, re.DirCount, re.Size));
        }
    }
}


// IDEA test a list of fullpaths.