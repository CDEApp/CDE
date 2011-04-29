using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using cdeLib;

namespace cde
{
    class Program
    {
        private const string ParamFind = "--find";
        private const string ParamGrep = "--grep";
        private const string ParamGreppath = "--greppath";
        private static List<string> findParams = new List<string>
                { ParamFind, ParamGrep, ParamGreppath };

        static void Main(string[] args)
        {
            var param0 = args[0].ToLowerInvariant();
            if (args.Length == 2 && args[0] == "--scan")
            {
                CreateCDECache(args[1]);
            }
            else if (args.Length == 2 && findParams.Contains(args[0].ToLowerInvariant()))
            {
                FindString(args[1], args[0].ToLowerInvariant());
            }
            else
            {
                Console.WriteLine("Usage: cde --scan <path>");
                Console.WriteLine("       scans path and creates a cache file.");
                Console.WriteLine("Usage: cde --find <string>");
                Console.WriteLine("       uses all cache files available searches for <string> as substring of on file name.");
                Console.WriteLine("Usage: cde --grep <regex>");
                Console.WriteLine("       uses all cache files available searches for <regex> as regex match on file name.");
                Console.WriteLine("Usage: cde --greppath <regex>");
                Console.WriteLine("       uses all cache files available searches for <regex> as regex match on full path to file name.");
            }
        }

        static void FindString(string find, string paramString)
        {
            switch (paramString)
            {
                case ParamGrep:
                case ParamGreppath:
                    var regexError = GetRegexErrorMessage(find);
                    if (!string.IsNullOrEmpty(regexError))
                    {
                        Console.WriteLine(regexError);
                        return;
                    }
                    break;
            }

            Regex regex;
            CommonEntry.ApplyToEntry matchFunc;
            var totalFound = 0u;
            switch (paramString)
            {
                case ParamFind:
                    matchFunc = delegate(string fullPath, DirEntry dirEntry)
                    {
                        if (dirEntry.Name.IndexOf(find, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            ++totalFound;
                            Console.WriteLine("found {0}", fullPath);
                        }
                    };
                    break;

                case ParamGrep:
                    regex = new Regex(find, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    matchFunc = delegate(string fullPath, DirEntry dirEntry)
                    {
                        if (regex.IsMatch(dirEntry.Name))
                        {
                            ++totalFound;
                            Console.WriteLine("found {0}", fullPath);
                        }
                    };
                    break;

                case ParamGreppath:
                    regex = new Regex(find, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    matchFunc = delegate(string fullPath, DirEntry dirEntry)
                    {
                        if (regex.IsMatch(fullPath))
                        {
                            ++totalFound;
                            Console.WriteLine("found {0}", fullPath);
                        }
                    };
                    break;

                default:
                    throw new ArgumentException(string.Format("Unknown parameter {0} to FindString", paramString));
            }

            Console.WriteLine("Searching for entries that contain \"{0}\"", find);
            var start = DateTime.UtcNow;
            var rootEntries = RootEntry.LoadCurrentDirCache();
            var end = DateTime.UtcNow;
            var loadTimeSpan = end - start;
            Console.WriteLine("Loaded {0} file(s) in {1:0.00} msecs", rootEntries.Count, loadTimeSpan.TotalMilliseconds);
            foreach (var rootEntry in rootEntries)
            {
                Console.WriteLine("Loaded File {0} with {1} entries.", rootEntry.DefaultFileName, rootEntry.DirCount + rootEntry.FileCount);
            }
            foreach (var rootEntry in rootEntries)
            {
                rootEntry.TraverseTree(rootEntry.RootPath, matchFunc);
            }

            if (totalFound > 0)
            {
                Console.WriteLine("Found a total of {0} entries. Containing the string \"{1}\"", totalFound, find);
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

        public static string GetRegexErrorMessage(string testPattern)
        {
            if ((testPattern != null) && (testPattern.Trim().Length > 0))
            {
                try
                {
                    Regex.Match("", testPattern);
                }
                catch (ArgumentException ae)
                {
                    // BAD PATTERN: Syntax error
                    return string.Format("Bad Regex: {0}.", ae.Message);
                }
            }
            else
            {
                //BAD PATTERN: Pattern is null or blank
                return "Bad Regex: Pattern is Null or Empty.";
            }
            return (string.Empty);
        }

        static void CreateCDECache(string path)
        {
            var re = new RootEntry();
            try
            {
                re.SimpleScanCountEvent = ScanEvery1000Entries;
                re.SimpleScanEndEvent = ScanEndofEntries;
                re.ExceptionEvent = PrintExceptions;

                re.PopulateRoot(path);

                re.SaveRootEntry();
                var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
                Console.WriteLine("Scanned Path {0}", re.RootPath);
                Console.WriteLine("Scan time {0:0.00} msecs", scanTimeSpan.TotalMilliseconds);
                Console.WriteLine("Saved Scanned Path {0}", re.DefaultFileName);
                Console.WriteLine("Files {0:0,0} Dirs {1:0,0} Total Size of Files {2:0,0}", re.FileCount, re.DirCount, re.Size);
            }
            catch (ArgumentException aex)
            {
                Console.WriteLine("Error: {0}", aex.Message);
            }
        }

        private static void PrintExceptions(string path, Exception ex)
        {
            Console.WriteLine("Exception {0}, Path \"{1}\"", ex.GetType(), path);
        }

        public static void ScanEvery1000Entries()
        {
            Console.Write(".");
        }

        public static void ScanEndofEntries()
        {
            Console.WriteLine("");
        }
    }
}


// IDEA test a list of fullpaths.