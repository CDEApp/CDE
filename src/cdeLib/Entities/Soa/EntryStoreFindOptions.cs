using System;

namespace cdeLib.Entities.Soa;

/// <summary>
/// Filter parameters for <see cref="EntryStoreSearch"/>, mirroring the subset of the tree-based
/// FindOptions that the cdeWin GUI search uses (pattern + name/path + file/folder + size/date/hour
/// ranges). Evaluated directly against the store's Size[] and ModifiedTicks[] arrays.
/// </summary>
public sealed class EntryStoreFindOptions
{
    public string Pattern { get; set; }
    public bool RegexMode { get; set; }
    public bool IncludePath { get; set; }
    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;

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
}
