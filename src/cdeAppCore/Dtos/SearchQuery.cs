using System;

namespace cdeAppCore.Dtos;

/// <summary>
/// A serializable search request — the frontend-agnostic equivalent of the WinForms search form's
/// inputs. Carries pattern + regex mode + path inclusion + file/folder inclusion + result limit, and
/// the optional size/date/hour/not-older-than range filters (each an enable flag plus its value).
/// Both <c>cdeWin</c> (in-process) and <c>cdeApi</c> (over the wire) construct the same query.
/// </summary>
public sealed class SearchQuery
{
    public string Pattern { get; set; } = "";
    public bool RegexMode { get; set; }
    public bool IncludePath { get; set; }
    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;

    /// <summary>Maximum number of result rows to return (matches the WinForms result-limit dropdown).</summary>
    public int LimitResultCount { get; set; } = 10000;

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
