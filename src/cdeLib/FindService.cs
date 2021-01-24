using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib
{
    public interface IFindService
    {
        void Find(string pattern, string param, IList<RootEntry> rootEntries);
        void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);
        bool IncludeFiles { get; set; }
        bool IncludeFolders { get; set; }
    }

    public class FindService : IFindService
    {
        public const string ParamFind = "--find";
        public const string ParamFindpath = "--findpath";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";

        public FindService()
        {
            IncludeFolders = true;
            IncludeFiles = true;
        }

        public bool IncludeFiles { get; set; }

        public bool IncludeFolders { get; set; }

        public void Find(string pattern, string param, IList<RootEntry> rootEntries)
        {
            var regexMode = param == ParamGrep || param == ParamGreppath;
            var includePath = param == ParamGreppath || param == ParamFindpath;
            Find(pattern, regexMode, includePath, rootEntries);
        }

        public void Find(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
        {
            var totalFound = 0L;
            var findOptions = new FindOptions
            {
                Pattern = pattern,
                RegexMode = regexMode,
                IncludePath = includePath,
                IncludeFiles = IncludeFiles,
                IncludeFolders = IncludeFolders,
                LimitResultCount = int.MaxValue,
                VisitorFunc = (p, d) =>
                {
                    ++totalFound;
                    Console.WriteLine(" {0}", p.MakeFullPath(d));
                    return true;
                },
            };

            findOptions.Find(rootEntries);

            Console.WriteLine(totalFound > 0
                ? $"Found a total of {totalFound} entries. Matching pattern \"{pattern}\""
                : "No entries found in cached information.");
        }
    }
}