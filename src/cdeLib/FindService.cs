using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib
{
    public interface IFindService
    {
        void StaticFind(string pattern, string param, IList<RootEntry> rootEntries);
        void StaticFind(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries);
    }

    public class FindService : IFindService
    {
        public const string ParamFind = "--find";
        public const string ParamFindpath = "--findpath";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamFindpath, ParamGrep, ParamGreppath };

        public void StaticFind(string pattern, string param, IList<RootEntry> rootEntries)
        {
            var regexMode = param == ParamGrep || param == ParamGreppath;
            var includePath = param == ParamGreppath || param == ParamFindpath;
            StaticFind(pattern, regexMode, includePath, rootEntries);
        }

        public void StaticFind(string pattern, bool regexMode, bool includePath, IList<RootEntry> rootEntries)
        {
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

            Console.WriteLine(totalFound > 0
                ? $"Found a total of {totalFound} entries. Matching pattern \"{pattern}\""
                : "No entries found in cached information.");
        }
    }
}
