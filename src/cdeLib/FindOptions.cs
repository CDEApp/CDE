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

        public FindOptions()
        {
            LimitResultCount = 10000;
            ProgressModifier = int.MaxValue; // huge m0n.
        }

        /// <summary>
        /// Called for every found entry.
        /// </summary>
        public TraverseFunc FoundFunc { get; set; }
        /// <summary>
        /// Called for reporting progress to caller.
        /// </summary>
        public Action<int, int> ProgressFunc { get; set; }
        public BackgroundWorker Worker { get; set; }

        public void Find(IEnumerable<RootEntry> rootEntries)
        {
            int[] limitCount = { LimitResultCount };
            if (ProgressFunc == null || ProgressModifier == 0)
            {   // dummy func and huge progressModifier so it wont call the progressFunc anyway.
                ProgressFunc = delegate { };
                ProgressModifier = int.MaxValue;
            }

            // ReSharper disable PossibleMultipleEnumeration
            var progressEnd = rootEntries.Sum(rootEntry => (int)rootEntry.DirCount + (int)rootEntry.FileCount);
            // ReSharper restore PossibleMultipleEnumeration
            var progressCount = new[] { 0 };
            ProgressFunc(progressCount[0], progressEnd);        // first progress right at start.

            TraverseFunc findFunc;
            if (FoundFunc == null)
            {
                return;
            }
            if (RegexMode)
            {
                var regex = new Regex(Pattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (IncludePath)
                {
                    findFunc = (p, d) =>
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
                                && (!NotOlderThanEnable || (NotOlderThanEnable && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                                && regex.IsMatch(p.MakeFullPath(d)))
                            {
                                if (!FoundFunc(p, d))
                                {
                                    return false;
                                }
                                if (--limitCount[0] <= 0)
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
                                && (!NotOlderThanEnable || (NotOlderThanEnable && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                                && regex.IsMatch(d.Path))
                            {
                                if (!FoundFunc(p, d))
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
                if (IncludePath)  // not sure this is useful to users.
                {
                    findFunc = (p, d) =>
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
                                && (!NotOlderThanEnable || (NotOlderThanEnable && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                                && p.MakeFullPath(d).IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!FoundFunc(p, d))
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
                                && (!NotOlderThanEnable || (NotOlderThanEnable && !d.IsModifiedBad && d.Modified >= NotOlderThan))
                                && d.Path.IndexOf(Pattern, StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                if (!FoundFunc(p, d))
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
            // ReSharper disable PossibleMultipleEnumeration
            CommonEntry.TraverseTreePair(rootEntries, findFunc);
            // ReSharper restore PossibleMultipleEnumeration
        }

    }
}