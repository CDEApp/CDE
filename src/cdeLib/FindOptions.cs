using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

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


        /// <summary>
        /// Called for every found entry.
        /// </summary>
        public TraverseFunc FoundFunc { get; set; }
        /// <summary>
        /// Called for reporting progress to caller.
        /// </summary>
        public Action<int, int> ProgressFunc { get; set; }
        public BackgroundWorker Worker { get; set; }
        public Func<CommonEntry, DirEntry, bool> PatternMatcher { get; set; }

        public FindOptions()
        {
            LimitResultCount = 10000;
            ProgressModifier = int.MaxValue; // huge m0n.
        }

        public void Find(IEnumerable<RootEntry> rootEntries)
        {
            if (FoundFunc == null)
            {
                return;
            }

            int[] limitCount = { LimitResultCount };
            if (ProgressFunc == null || ProgressModifier == 0)
            {   // dummy func and huge progressModifier so wont call progressFunc anyway.
                ProgressFunc = delegate { };
                ProgressModifier = int.MaxValue;
            }

            // ReSharper disable PossibleMultipleEnumeration
            var progressEnd = rootEntries.TotalFileEntries();
            // ReSharper restore PossibleMultipleEnumeration
            var progressCount = new[] { 0 };
            ProgressFunc(progressCount[0], progressEnd);        // Start of process Progress report.

            if (RegexMode)
            {
                var regex = new Regex(Pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (IncludePath)
                {
                    PatternMatcher = (p, d) => regex.IsMatch(p.MakeFullPath(d));
                }
                else
                {
                    PatternMatcher = (p, d) => regex.IsMatch(d.Path);
                }
            }
            else
            {
                if (IncludePath)
                {
                    PatternMatcher = (p, d) => 
                        p.MakeFullPath(d).IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0;
                }
                else
                {
                    PatternMatcher = (p, d) =>
                        d.Path.IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0;
                } 
            }

            TraverseFunc findFunc = (p, d) =>
            {
                ++progressCount[0];
                if (progressCount[0] % ProgressModifier == 0)
                {
                    ProgressFunc(progressCount[0], progressEnd);
                    // only check for cancel on progress modifier.
                    if (Worker != null && Worker.CancellationPending)
                    {
                        return false;   // end the find.
                    }
                }
                if ((d.IsDirectory && IncludeFolders) 
                    || (!d.IsDirectory && IncludeFiles))
                {
                    if (   (!FromSizeEnable || (FromSizeEnable && d.Size >= FromSize))
                        && (!ToSizeEnable || (ToSizeEnable && d.Size <= ToSize))
                        && (!FromDateEnable || (FromDateEnable && !d.IsModifiedBad && d.Modified >= FromDate))
                        && (!ToDateEnable || (ToDateEnable && !d.IsModifiedBad && d.Modified <= ToDate))
                        && (!FromHourEnable || (FromHourEnable && !d.IsModifiedBad 
                                                && FromHour.TotalSeconds <= d.Modified.TimeOfDay.TotalSeconds))
                        && (!ToHourEnable || (ToHourEnable && !d.IsModifiedBad 
                                                && ToHour.TotalSeconds >= d.Modified.TimeOfDay.TotalSeconds))
                        && (!NotOlderThanEnable || (NotOlderThanEnable 
                                                && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                        && PatternMatcher(p, d))
                    {
                        if (!FoundFunc(p, d)
                            || --limitCount[0] <= 0)
                        {
                            return false;   // end the find.
                        }
                    }
                }
                return true;
            };
            // ReSharper disable PossibleMultipleEnumeration
            CommonEntry.TraverseTreePair(rootEntries, findFunc);
            // ReSharper restore PossibleMultipleEnumeration
        }
    }

    // ReSharper disable InconsistentNaming
    public static class IEnumerableRootEntryExtension
    {
        public static int TotalFileEntries(this IEnumerable<RootEntry> rootEntries)
        {
            return rootEntries.Sum(rootEntry => (int)rootEntry.DirCount + (int)rootEntry.FileCount);
        }
    }
    // ReSharper restore InconsistentNaming
}