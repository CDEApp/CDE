using System;
using System.Collections.Generic;
using ProtoBuf;

namespace cdeWin.Cfg;

[ProtoContract]
public class Configuration
{
    public const string Magic = "cdeWinViewConfig";
    [ProtoMember(1)]
    public string MagicVersion = Magic + "0001";
    [ProtoMember(2)]
    public WindowConfig MainWindowConfig;
    [ProtoMember(3)]
    public ListViewConfig SearchResultListView;
    [ProtoMember(4)]
    public ListViewConfig DirectoryListView;
    [ProtoMember(5)]
    public ListViewConfig CatalogListView;

    // [ProtoMember(6)] public int PatternHistoryLength = DefaultPatternHistoryLength;
    [ProtoMember(7)]
    public List<string> PreviousPatternHistory;
    [ProtoMember(8)]
    public float DirectoryPaneSplitterRatio;
    [ProtoMember(9)]
    public string Pattern;
    [ProtoMember(10)]
    public bool RegexMode;
    [ProtoMember(11)]
    public bool IncludePath;
    [ProtoMember(12)]
    public int FindEntryFilter;
    [ProtoMember(13)]
    public bool IsAdvancedSearchMode;
    [ProtoMember(14)]
    public int LimitResultCountIndex;
    [ProtoMember(15)]
    public int FromSizeDropDownIndex;
    [ProtoMember(16)]
    public int ToSizeDropDownIndex;
    [ProtoMember(17)]
    public int NotOlderThanDropDownIndex;
    [ProtoMember(18)]
    public decimal FromSizeField;
    [ProtoMember(19)]
    public decimal ToSizeField;
    [ProtoMember(20)]
    public decimal NotOlderThanField;
    [ProtoMember(21)]
    public DateTime FromDateValue;
    [ProtoMember(22)]
    public DateTime ToDateValue;
    [ProtoMember(23)]
    public DateTime FromHourValue;
    [ProtoMember(24)]
    public DateTime ToHourValue;
    [ProtoMember(25)]
    public int PatternHistoryMaximum;
}