using System.Collections.Generic;

namespace cdeAppCore.Dtos;

/// <summary>One folder pair to compare. Each side is an entry ref string, <c>{catalogId}-{entryIndex}</c>.</summary>
public sealed class ComparePairDto
{
    public string Left { get; set; }

    public string Right { get; set; }
}

/// <summary>Body of <c>POST /compare/summaries</c>.</summary>
public sealed class CompareSummariesRequest
{
    public List<ComparePairDto> Pairs { get; set; }
}

/// <summary>
/// What lies below one folder pair. Counts cover files and mismatched-kind pairs; a folder pair is a
/// container and is never counted itself. A one-sided entry (file or folder) counts once and is not descended.
/// </summary>
public sealed class CompareSummaryDto
{
    public int SameFiles { get; set; }

    public int DifferentFiles { get; set; }

    /// <summary>Different pairs whose left modified time is later than the right by more than the tolerance.</summary>
    public int LeftNewerFiles { get; set; }

    public int RightNewerFiles { get; set; }

    public int LeftOnly { get; set; }

    public int RightOnly { get; set; }
}

/// <summary>Response of <c>POST /compare/summaries</c>: one summary per request pair, in request order.</summary>
public sealed class CompareSummariesResponse
{
    public List<CompareSummaryDto> Summaries { get; set; } = [];
}
