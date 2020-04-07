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
        private static IList<RootEntry> _rootEntries;
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
                VisitorFunc = (p, d) =>
                {
                    ++totalFound;
                    Console.WriteLine(" {0}", p.MakeFullPath(d));
                    return true;
                },
            };
            findOptions.Find(_rootEntries);
            if (totalFound >  0)
            {
                Console.WriteLine($"Found a total of {totalFound} entries. Matching pattern \"{pattern}\"");
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
                Console.WriteLine($"Loaded {_rootEntries.Count} file(s) in {loadTimeSpan.TotalMilliseconds:0.00} msecs");
                foreach (var rootEntry in _rootEntries)
                {
                    Console.WriteLine(
                        $"Loaded File {rootEntry.DefaultFileName} with {rootEntry.DirEntryCount + rootEntry.FileEntryCount} entries.");
                }
            }
        }
    }
}
