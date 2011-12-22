using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using cdeLib.Infrastructure;

namespace cdeLib
{
    public enum TraverseExit { Complete, LimitCountReached, FoundFuncExit }

    public class FindOptions
    {
        public string Pattern { get; set; }
        public bool RegexMode { get; set; }
        public bool IncludePath { get; set; }
        public bool IncludeFiles { get; set; }
        public bool IncludeFolders { get; set; }
        public int LimitResultCount { get; set; }

        public FindOptions()
        {
            LimitResultCount = 10000;
        }

        /// <summary>
        /// Called for every found entry.
        /// </summary>
        public CommonEntry.TraverseFunc FoundFunc { get; set; }
    }

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

        public static void Find2(string pattern, string param)
        {
            var regexMode = param == ParamGrep || param == ParamGreppath;
            var includePath = param == ParamGreppath || param == ParamFindpath;
            Find2(pattern, regexMode, includePath);
        }

        public static void Find2(string pattern, bool regexMode, bool includePath)
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
            TraverseTreeFind(_rootEntries, findOptions);
            if (totalFound >  0)
            {
                Console.WriteLine("Found a total of {0} entries. Matching pattern \"{1}\"", totalFound, pattern);
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

        public static void TraverseTreeFind(IEnumerable<RootEntry> rootEntries, FindOptions options)
        {
            int[] limitCount = { options.LimitResultCount };
            var foundFunc = options.FoundFunc;
            CommonEntry.TraverseFunc findFunc;
            if (foundFunc == null)
            {
                return;
            }
            if (options.RegexMode)
            {
                var regex = new Regex(options.Pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (options.IncludePath)
                {
                    findFunc = (p, d) =>
                    {
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (regex.IsMatch(p.MakeFullPath(d)))
                            {
                                if (!foundFunc(p, d))
                                {
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;
                    };
                }
                else
                {
                    findFunc = (p, d) =>
                    {
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (regex.IsMatch(d.Path))
                            {
                                if (!foundFunc(p, d))
                                {
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;
                    };
                }
            }
            else
            {
                if (options.IncludePath)  // not sure this is useful to users.
                {
                    findFunc = (p, d) =>
                    {
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (p.MakeFullPath(d).IndexOf(options.Pattern,
                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!foundFunc(p, d))
                                {
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;
                    };
                }
                else
                {
                    findFunc = (p, d) =>
                    {
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (d.Path.IndexOf(options.Pattern,
                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!foundFunc(p, d))
                                {
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;
                    };
                }
            }
            foreach (var root in rootEntries)
            {
                root.TraverseTreePair(findFunc);
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
