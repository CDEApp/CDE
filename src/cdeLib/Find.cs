using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace cdeLib
{
    public class Find
    {
        private const string ParamFind = "--find";
        private const string ParamGrep = "--grep";
        private const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamGrep, ParamGreppath };

        public static void FindString(string find, string paramString)
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
            Action<string, DirEntry> matchFunc;
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
                    return string.Format("Bad Regex: {0}.", ae.Message);
                }
            }
            else
            {
                return "Bad Regex: Pattern is Null or Empty.";
            }
            return (string.Empty);
        }
    }

}
