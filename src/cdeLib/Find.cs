using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace cdeLib
{
    public enum TraverseExit { Complete, LimitCountReached, FoundFuncExit, WorkerCancel }

    public class FindOptions
    {
        public string Pattern { get; set; }
        public bool RegexMode { get; set; }
        public bool IncludePath { get; set; }
        public bool IncludeFiles { get; set; }
        public bool IncludeFolders { get; set; }
        public int LimitResultCount { get; set; } // consider making this a multiple of ProgressModifier
        public int ProgressModifier { get; set; }
        //public int ProgressEnd { get; set; }

        public FindOptions()
        {
            LimitResultCount = 10000;
            ProgressModifier = int.MaxValue; // huge m0n.
            //ProgressEnd = int.MaxValue;
        }

        /// <summary>
        /// Called for every found entry.
        /// </summary>
        public TraverseFunc FoundFunc { get; set; }
        public Action<int, int> ProgressFunc { get; set; }
        public BackgroundWorker Worker { get; set; }
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
            var findContinue = true;
            var worker = options.Worker;
            var foundFunc = options.FoundFunc;
            var progressFunc = options.ProgressFunc;
            var progressModifier = options.ProgressModifier;
            if (progressFunc == null || progressModifier == 0)
            {   // dummy func and huge progressModifier so it wont call the progressFunc anyway.
                progressFunc = delegate { };
                progressModifier = int.MaxValue;
            }

            // ReSharper disable PossibleMultipleEnumeration
            var progressEnd = rootEntries.Sum(rootEntry => (int) rootEntry.DirCount + (int) rootEntry.FileCount);
            // ReSharper restore PossibleMultipleEnumeration
            var progressCount = new[] { 0 };

            TraverseFunc findFunc;
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
                        ++progressCount[0];
                        if (progressCount[0] % progressModifier == 0)
                        {
                            progressFunc(progressCount[0], progressEnd);
                            // only check for cancel on progress modifier.
                            if (worker != null && worker.CancellationPending)
                            {
                                findContinue = false;
                                return false;   // end the find.
                            }
                        }
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (regex.IsMatch(p.MakeFullPath(d)))
                            {
                                if (!foundFunc(p, d))
                                {
                                    findContinue = false;
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    findContinue = false;
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
                        ++progressCount[0];
                        if (progressCount[0] % progressModifier == 0)
                        {
                            progressFunc(progressCount[0], progressEnd);
                            // only check for cancel on progress modifier.
                            if (worker != null && worker.CancellationPending)
                            {
                                findContinue = false;
                                return false;   // end the find.
                            }
                        }
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (regex.IsMatch(d.Path))
                            {
                                if (!foundFunc(p, d))
                                {
                                    findContinue = false;
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    findContinue = false;
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
                        ++progressCount[0];
                        if (progressCount[0] % progressModifier == 0)
                        {
                            progressFunc(progressCount[0], progressEnd);
                            // only check for cancel on progress modifier.
                            if (worker != null && worker.CancellationPending)
                            {
                                findContinue = false;
                                return false;   // end the find.
                            }
                        }
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (p.MakeFullPath(d).IndexOf(options.Pattern,
                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!foundFunc(p, d))
                                {
                                    findContinue = false;
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    findContinue = false;
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
                        ++progressCount[0];
                        if (progressCount[0] % progressModifier == 0)
                        {
                            progressFunc(progressCount[0], progressEnd);
                            // only check for cancel on progress modifier.
                            if (worker != null && worker.CancellationPending)
                            {
                                findContinue = false;
                                return false;   // end the find.
                            }
                        }
                        if ((d.IsDirectory && options.IncludeFolders)
                            || (!d.IsDirectory && options.IncludeFiles))
                        {
                            if (d.Path.IndexOf(options.Pattern,
                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!foundFunc(p, d))
                                {
                                    findContinue = false;
                                    return false;
                                }
                                if (--limitCount[0] <= 1)
                                {
                                    findContinue = false;
                                    return false;
                                }
                            }
                        }
                        return true;
                    };
                }
            }
            // ReSharper disable PossibleMultipleEnumeration
            foreach (var root in rootEntries)
            {
                if (findContinue)
                {
                    root.TraverseTreePair(findFunc);
                }
            }
            // ReSharper restore PossibleMultipleEnumeration
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
