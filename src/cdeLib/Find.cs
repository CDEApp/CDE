using System;
using System.Collections.Generic;

namespace cdeLib
{
    public static class Find
    {
        public const string ParamFind = "--find";
        public const string ParamFindpath = "--findpath";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamFindpath, ParamGrep, ParamGreppath };

        // ReSharper disable InconsistentNaming
        private static List<RootEntry> _rootEntries;
        // ReSharper restore InconsistentNaming

        public static void StaticFind(string pattern, string param)
        {
            var regexMode = param == ParamGrep || param == ParamGreppath;
            var includePath = param == ParamGreppath || param == ParamFindpath;
            StaticFind(pattern, regexMode, includePath);
        }

        public static void StaticFind(string pattern, bool regexMode, bool includePath)
        {
            GetDirCache();
            var totalFound = 0L;
            var findOptions = new FindOptions
            {
                Pattern = pattern,
                RegexMode = regexMode,
                IncludePath = includePath,
                IncludeFiles = true,
                IncludeFolders = true,
                LimitResultCount = int.MaxValue,
                FoundFunc = (p, d) =>
                {
                    ++totalFound;
                    Console.WriteLine(" {0}", p.MakeFullPath(d));
                    return true;
                },
            };
            findOptions.Find(_rootEntries);
            if (totalFound >  0)
            {
                Console.WriteLine("Found a total of {0} entries. Matching pattern \"{1}\"", totalFound, pattern);
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
    }
}
