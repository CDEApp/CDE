using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using cdeLib.Entities;
using RootEntry = cdeLib.Entities.RootEntry;
using TraverseFunc = cdeLib.Entities.TraverseFunc;

namespace cdeLib
{
    public class FindOptions
    {
        public string Pattern { get; set; }
        public bool RegexMode { get; set; }
        public bool IncludePath { get; set; }
        public bool IncludeFiles { get; set; }
        public bool IncludeFolders { get; set; }
        public int LimitResultCount { get; set; } // consider making this a multiple of ProgressModifier
        public int ProgressModifier { get; set; }

        public bool FromSizeEnable { get; set; }
        public long FromSize { get; set; }
        public bool ToSizeEnable { get; set; }
        public long ToSize { get; set; }
        public bool FromDateEnable { get; set; }
        public DateTime FromDate { get; set; }
        public bool ToDateEnable { get; set; }
        public DateTime ToDate { get; set; }
        public bool FromHourEnable { get; set; }
        public TimeSpan FromHour { get; set; }
        public bool ToHourEnable { get; set; }
        public TimeSpan ToHour { get; set; }
        public bool NotOlderThanEnable { get; set; }
        public DateTime NotOlderThan { get; set; }
        public int ProgressEnd { get; set; }
        public int ProgressCount => _progressCount[0];
        private readonly int[] _progressCount = new[] { 0 };
        public int SkipCount { get; set; }

        /// <summary>
        /// Called for every entry that matches predicate entry.
        /// </summary>
        public TraverseFunc VisitorFunc { get; set; }

        /// <summary>
        /// Called for reporting progress to caller.
        /// </summary>
        public Action<int, int> ProgressFunc { get; set; }

        public BackgroundWorker Worker { get; set; }
        public Func<ICommonEntry, ICommonEntry, bool> PatternMatcher { get; set; }

        private readonly object _countLock = new object();

        public FindOptions()
        {
            LimitResultCount = 10000;
            ProgressModifier = int.MaxValue; // huge m0n.
            IncludeFiles = true;
            IncludeFolders = true;
        }

        public void Find(IEnumerable<RootEntry> rootEntries)
        {
            const bool useParallel = true;

            if (VisitorFunc == null)
            {
                return;
            }

            int[] limitCount = { LimitResultCount };
            if (ProgressFunc == null || ProgressModifier == 0)
            {
                // dummy func and huge progressModifier so wont call progressFunc anyway.
                ProgressFunc = delegate { };
                ProgressModifier = int.MaxValue;
            }

            // ReSharper disable PossibleMultipleEnumeration
            ProgressEnd = rootEntries.TotalFileEntries();
            // ReSharper restore PossibleMultipleEnumeration
            ProgressFunc(_progressCount[0], ProgressEnd); // Start of process Progress report.
            PatternMatcher = GetPatternMatcher();

            var findFunc = GetFindFunc(_progressCount, limitCount);
            // ReSharper disable PossibleMultipleEnumeration

            if (useParallel)
            {
                Parallel.ForEach(rootEntries, (rootEntry) =>
                {
                    //TODO: Parallel breaks the progress percentage, need to fix.
                    EntryHelper.TraverseTreePair(new List<ICommonEntry> { rootEntry }, findFunc);
                });
            }
            else
            {
                foreach (var rootEntry in rootEntries)
                {
                    EntryHelper.TraverseTreePair(new List<ICommonEntry> { rootEntry }, findFunc, rootEntry);
                }
            }

            ProgressFunc(_progressCount[0], ProgressEnd); // end of Progress
        }

        public Func<ICommonEntry, ICommonEntry, bool> GetPatternMatcher()
        {
            Func<ICommonEntry, ICommonEntry, bool> matcher;
            if (RegexMode)
            {
                var regex = new Regex(Pattern,
                    RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                matcher = (p, d) => regex.IsMatch(d.Path);
                if (IncludePath)
                {
                    matcher = (p, d) => regex.IsMatch(p.MakeFullPath(d));
                }
            }
            else
            {
                matcher = (p, d) => d.Path.IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0;
                if (IncludePath)
                {
                    matcher = (p, d) =>
                        p.MakeFullPath(d).IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0;
                }
            }

            return matcher;
        }

        public TraverseFunc GetFindFunc(int[] progressCount, int[] limitCount)
        {
            var findPredicate = GetFindPredicate();

            bool FindFunc(ICommonEntry p, ICommonEntry dirEntry)
            {
                lock (_countLock)
                {
                    ++progressCount[0];
                    if (progressCount[0] <= SkipCount)
                    {
                        // skip enforced
                        return true;
                    }

                    if (progressCount[0] % ProgressModifier == 0) //wiggle room for multithreaded updates.
                    {
                        ProgressFunc(progressCount[0], ProgressEnd);
                        // only check for cancel on progress modifier.
                        if (Worker?.CancellationPending == true)
                        {
                            return false; // end the find.
                        }
                    }

                    if (findPredicate(p, dirEntry))
                    {
                        if (!VisitorFunc(p, dirEntry) || --limitCount[0] <= 0)
                        {
                            return false; // end the find.
                        }
                    }
                }
                return true;
            }
            return FindFunc;
        }

        public TraverseFunc GetFindPredicate()
        {
            return (p, d) =>
                ((d.IsDirectory && IncludeFolders) || (!d.IsDirectory && IncludeFiles))
                && (!FromSizeEnable || (FromSizeEnable && d.Size >= FromSize))
                && (!ToSizeEnable || (ToSizeEnable && d.Size <= ToSize))
                && (!FromDateEnable || (FromDateEnable && !d.IsModifiedBad && d.Modified >= FromDate))
                && (!ToDateEnable || (ToDateEnable && !d.IsModifiedBad && d.Modified <= ToDate))
                && (!FromHourEnable || (FromHourEnable && !d.IsModifiedBad
                                                       && FromHour.TotalSeconds <= d.Modified.TimeOfDay.TotalSeconds))
                && (!ToHourEnable || (ToHourEnable && !d.IsModifiedBad
                                                   && ToHour.TotalSeconds >= d.Modified.TimeOfDay.TotalSeconds))
                && (!NotOlderThanEnable || (NotOlderThanEnable
                                            && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                && PatternMatcher(p, d);
        }
    }
}