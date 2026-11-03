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

    /// <summary>
    /// Treat <see cref="Pattern"/> as an Everything-style query (<see cref="EverythingQuery"/>) instead of a plain
    /// substring. Takes precedence over <see cref="RegexMode"/>. The pattern must be valid (check with
    /// <see cref="EverythingQuery.Parse"/> first); a malformed one throws <see cref="EverythingQueryException"/>.
    /// </summary>
    public bool EverythingMode { get; set; }

    /// <summary>
    /// The duplicate counts for the dupe: functions of an Everything query, counted over every catalog of the search
    /// (see <see cref="DuplicateIndex"/>). Null counts duplicates within the one catalog being searched.
    /// </summary>
    public DuplicateIndex Duplicates { get; set; }

    /// <summary>
    /// The file name of the catalog being searched, for <c>catalog:</c> in an Everything query. Null uses the catalog's
    /// own file name (see <see cref="CatalogNames.FileName"/>).
    /// </summary>
    public string CatalogName { get; set; }

    /// <summary>The clock for <c>today</c> and spans such as <c>3days</c> in an Everything query. Null is the system clock.</summary>
    public TimeProvider Clock { get; set; }
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
