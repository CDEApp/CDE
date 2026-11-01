using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace cdeMemProbe;

/// <summary>One fixed search query and how it performed: first run in a fresh process, then the warm median.</summary>
public sealed record QueryTiming(
    string Name,
    string Mode,
    string Pattern,
    double ColdMs,
    double WarmMedianMs,
    long Matches);

/// <summary>
/// The stable <c>perf.json</c> written by <c>cdeMemProbe --report</c>. Bump <see cref="CurrentSchemaVersion"/>
/// when a field changes meaning; <see cref="PerfComparison"/> will not gate across versions.
/// </summary>
public sealed record PerfReport
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Commit { get; init; } = "unknown";
    public string Os { get; init; } = "";
    public long FixtureEntries { get; init; }
    public int Seed { get; init; }

    /// <summary>Wall time of the real <c>cde scan</c>; null when the .cdex was written directly (n/a).</summary>
    public double? ScanMs { get; init; }

    public long CdexBytes { get; init; }

    /// <summary>False when the page cache could not be dropped, so cold numbers are not truly cold.</summary>
    public bool ColdCacheDropped { get; init; }

    /// <summary>Peak working set (RSS) of the process that mapped the .cdex and ran the searches.</summary>
    public long PeakRssBytes { get; init; }

    public List<QueryTiming> Queries { get; init; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static PerfReport FromJson(string json) =>
        JsonSerializer.Deserialize<PerfReport>(json, JsonOptions)
        ?? throw new InvalidDataException("perf.json is empty");

    public static PerfReport Load(string path) => FromJson(File.ReadAllText(path));
}
