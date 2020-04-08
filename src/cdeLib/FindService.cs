using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib
{
    public interface IFindService
    {
        void StaticFind(string pattern, string param, IList<RootEntry> rootEntries);
        void StaticFind(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);
        void GetDirCache(IList<RootEntry> rootEntries);
    }

    public class FindService : IFindService
    {
        public const string ParamFind = "--find";
        public const string ParamFindpath = "--findpath";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamFindpath, ParamGrep, ParamGreppath };

        // ReSharper disable InconsistentNaming
        private static IList<RootEntry> _rootEntries;
        // ReSharper restore InconsistentNaming

        public void StaticFind(string pattern, string param, IList<RootEntry> rootEntries)
        {
            var regexMode = param == ParamGrep || param == ParamGreppath;
            var includePath = param == ParamGreppath || param == ParamFindpath;
            StaticFind(pattern, regexMode, includePath, rootEntries);
        }

        public void StaticFind(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
        {
            //GetDirCache(rootEntries);
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
            findOptions.Find(rootEntries);
            if (totalFound >  0)
            {
                Console.WriteLine($"Found a total of {totalFound} entries. Matching pattern \"{pattern}\"");
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

        [Obsolete("Pass in value as parameter")]
        public void GetDirCache(IList<RootEntry> rootEntries)
        {
            if (_rootEntries == null)
            {
                var start = DateTime.UtcNow;
                _rootEntries = rootEntries;
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
