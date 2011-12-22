using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using cdeLib.Infrastructure;

namespace cdeLib
{
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
    }

    public static class Find
    {
        public const string ParamFind = "--find";
        public const string ParamFindpath = "--findpath";
        public const string ParamGrep = "--grep";
        public const string ParamGreppath = "--greppath";
        public static readonly List<string> FindParams = new List<string> { ParamFind, ParamFindpath, ParamGrep, ParamGreppath };

        // ReSharper disable InconsistentNaming
        private static uint _totalFound;
        private static Regex _regex;
        private static string _find;
        private static List<RootEntry> _rootEntries;
        // ReSharper restore InconsistentNaming

        public static List<PairDirEntry> GetSearchHitsR(IEnumerable<RootEntry> rootEntries, FindOptions options)
        {
            int[] limitCount = {options.LimitResultCount};
            var list = new List<PairDirEntry>();
            if (options.RegexMode)
            {
                var regex = new Regex(options.Pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (options.IncludePath)
                {
                    foreach (var root in rootEntries)
                    {
                        root.TraverseTreePair((p, d) =>
                            {
                                if ((d.IsDirectory && options.IncludeFolders)
                                    || (!d.IsDirectory && options.IncludeFiles))
                                {
                                    if (regex.IsMatch(p.MakeFullPath(d)))
                                    {
                                        list.Add(new PairDirEntry(p, d));
                                        if (--limitCount[0] <= 1)
                                        {
                                            return false;
                                        }
                                    }
                                }
                                return true;
                            });
                    }
                }
                else
                {
                    foreach (var root in rootEntries)
                    {
                        root.TraverseTreePair((p, d) =>
                            {
                                if ((d.IsDirectory && options.IncludeFolders)
                                    || (!d.IsDirectory && options.IncludeFiles))
                                {
                                    if (regex.IsMatch(d.Path))
                                    {
                                        list.Add(new PairDirEntry(p, d));
                                        if (--limitCount[0] <= 1)
                                        {
                                            return false;
                                        }
                                    }
                                }
                                return true;
                            });
                    }
                }
            }
            else
            {
                if (options.IncludePath)  // not sure this is useful to users.
                {
                    foreach (var root in rootEntries)
                    {
                        root.TraverseTreePair((p, d) =>
                            {
                                if ((d.IsDirectory && options.IncludeFolders)
                                    || (!d.IsDirectory && options.IncludeFiles))
                                {
                                    if (p.MakeFullPath(d).IndexOf(options.Pattern,
                                          StringComparison.InvariantCultureIgnoreCase) >= 0)
                                    {
                                        list.Add(new PairDirEntry(p, d));
                                        if (--limitCount[0] <= 1)
                                        {
                                            return false;
                                        }
                                    }
                                }
                                return true;
                            });
                    }
                }
                else
                {
                    foreach (var root in rootEntries)
                    {
                        root.TraverseTreePair((p, d) =>
                            {
                                if ((d.IsDirectory && options.IncludeFolders)
                                    || (!d.IsDirectory && options.IncludeFiles))
                                {
                                    if (d.Path.IndexOf(options.Pattern, 
                                          StringComparison.InvariantCultureIgnoreCase) >= 0)
                                    {
                                        list.Add(new PairDirEntry(p, d));
                                        if (--limitCount[0] <= 1)
                                        {
                                            return false;
                                        }
                                    }
                                }
                                return true;
                            });
                    }
                }
            }
            return list;
        }

        public static IEnumerable<PairDirEntry> GetSearchHits(IEnumerable<RootEntry> rootEntries, string pattern, bool regexMode, bool includePath)
        {
            var pairDirEntries = CommonEntry.GetPairDirEntries(rootEntries);

            if (regexMode)
            {
                var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (includePath)
                {
                    foreach (var pairDirEntry in pairDirEntries)
                    {
                        //if (pairDirEntry.RootDE == null) { throw new Exception("Oops RootDE null "); }
                        if (regex.IsMatch(pairDirEntry.FullPath))
                        {
                            yield return pairDirEntry;
                        }
                    }
                }
                else
                {
                    foreach (var pairDirEntry in pairDirEntries)
                    {
                        //if (pairDirEntry.RootDE == null) { throw new Exception("Oops RootDE null "); }
                        if (regex.IsMatch(pairDirEntry.ChildDE.Path))
                        {
                            yield return pairDirEntry;
                        }
                    }
                }
            }
            else
            {
                if (includePath)  // not sure this is useful to users.
                {
                    foreach (var pairDirEntry in pairDirEntries)
                    {
                        //if (pairDirEntry.RootDE == null) { throw new Exception("Oops RootDE null "); }
                        if (pairDirEntry.FullPath.IndexOf(pattern, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            yield return pairDirEntry;
                        }
                    }
                }
                else
                {
                    foreach (var pairDirEntry in pairDirEntries)
                    {
                        //if (pairDirEntry.RootDE == null) { throw new Exception("Oops RootDE null "); }
                        if (pairDirEntry.ChildDE.Path.IndexOf(pattern, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            yield return pairDirEntry;
                        }
                    }
                }
            }
            yield break;
        }


        public static void FindString2(string find, string paramString)
        {
            GetDirCache();
            Console.WriteLine("Searching for entries that contain \"{0}\"", find);
            var totalFound = 0L;
            var regexMode = paramString == ParamGrep || paramString == ParamGreppath;
            var includePath = paramString == ParamGreppath || paramString == ParamFindpath;

            var e = GetSearchHits(_rootEntries, find, regexMode, includePath);
            foreach (var pairDirEntry in e)
            {
                ++totalFound;
                Console.WriteLine("found {0}", pairDirEntry.FullPath);
            }

            if (totalFound > 0)
            {
                Console.WriteLine("Found a total of {0} entries. Matching string \"{1}\"", totalFound, find);
            }
            else
            {
                Console.WriteLine("No entries found in cached information.");
            }
        }

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

            CommonEntry.TraverseFunc matchAction;
            _totalFound = 0u;
            switch (paramString)
            {
                case ParamFind:
                    matchAction = MatchSubstringName;
                    break;

                case ParamFindpath:
                    matchAction = MatchSubstringFullPath;
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
                ((CommonEntry) rootEntry).TraverseTreePair(matchAction);
            }

            if (_totalFound > 0)
            {
                Console.WriteLine("Found a total of {0} entries. Matching string \"{1}\"", _totalFound, find);
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

        private static bool MatchSubstringName(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (dirEntry.Path.IndexOf(_find, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                ++_totalFound;
                var fullPath = CommonEntry.MakeFullPath(parentEntry, dirEntry);
                Console.WriteLine("found {0}", fullPath);
            }
            return true;
        }

        private static bool MatchSubstringFullPath(CommonEntry parentEntry, DirEntry dirEntry)
        {
            var fullPath = CommonEntry.MakeFullPath(parentEntry, dirEntry);
            if (fullPath.IndexOf(_find, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                ++_totalFound;
                Console.WriteLine("found {0}", fullPath);
            }
            return true;
        }

        private static bool MatchRegexName(CommonEntry parentEntry, DirEntry dirEntry)
        {
            if (_regex.IsMatch(dirEntry.Path))
            {
                ++_totalFound;
                var fullPath = CommonEntry.MakeFullPath(parentEntry, dirEntry);
                Console.WriteLine("found {0}", fullPath);
            }
            return true;
        }

        private static bool MatchRegexFullPath(CommonEntry parentEntry, DirEntry dirEntry)
        {
            var fullPath = CommonEntry.MakeFullPath(parentEntry, dirEntry);
            if (_regex.IsMatch(fullPath))
            {
                ++_totalFound;
                Console.WriteLine("found {0}", fullPath);
            }
            return true;
        }
    }
}
