using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public class Find
    {
        public const string ParamFind = "--find";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamGrep, ParamGreppath };

        private static uint _totalFound;
        private static Regex _regex;
        private static string _find;
        private static List<RootEntry> _rootEntries;

        public static void FindString(string find, string paramString)
        {
            _find = find;
            _regex = null;

            switch (paramString)
            {
                case ParamGrep:
                case ParamGreppath:
                    var regexError = RegexHelper.GetRegexErrorMessage(find);
                    if (!string.IsNullOrEmpty(regexError))
                    {
                        Console.WriteLine(regexError);
                        return;
                    }
                    _regex = new Regex(find, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    break;
            }

            Action<string, DirEntry> matchAction;
            _totalFound = 0u;
            switch (paramString)
            {
                case ParamFind:
                    matchAction = MatchSubstringName;
                    break;

                case ParamGrep:
                    matchAction = MatchRegexName;
                    break;

                case ParamGreppath:
                    matchAction = MatchRegexFullPath;
                    break;

                default:
                    throw new ArgumentException(string.Format("Unknown parameter \"{0}\" to FindString", paramString));
            }

            Console.WriteLine("Searching for entries that contain \"{0}\"", find);
            GetDirCache();
            foreach (var rootEntry in _rootEntries)
            {
                rootEntry.TraverseTree(rootEntry.RootPath, matchAction);
            }

            if (_totalFound > 0)
            {
                Console.WriteLine("Found a total of {0} entries. Containing the string \"{1}\"", _totalFound, find);
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

        public static void GetDirCache()
        {
            if (_rootEntries == null)
            {
                var start = DateTime.UtcNow;
                _rootEntries = RootEntry.LoadCurrentDirCache();
                var end = DateTime.UtcNow;
                var loadTimeSpan = end - start;
                Console.WriteLine("Loaded {0} file(s) in {1:0.00} msecs", _rootEntries.Count, loadTimeSpan.TotalMilliseconds);
                foreach (var rootEntry in _rootEntries)
                {
                    Console.WriteLine("Loaded File {0} with {1} entries.", rootEntry.DefaultFileName, rootEntry.DirCount + rootEntry.FileCount);
                }
            }
        }

        private static void MatchSubstringName(string fullPath, DirEntry dirEntry)
        {
            if (dirEntry.Name.IndexOf(_find, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                ++_totalFound;
                Console.WriteLine("found {0}", fullPath);
            }
        }

        private static void MatchRegexName(string fullPath, DirEntry dirEntry)
        {
            if (_regex.IsMatch(dirEntry.Name))
            {
                ++_totalFound;
                Console.WriteLine("found {0}", fullPath);
            }
        }

        private static void MatchRegexFullPath(string fullPath, DirEntry dirEntry)
        {
            if (_regex.IsMatch(fullPath))
            {
                ++_totalFound;
                Console.WriteLine("found {0}", fullPath);
            }
        }
    }
}
