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
/// What lies below one folder pair. The file counts cover files and mismatched-kind pairs. The folder counts
/// cover both-sides folder pairs nested at any depth below the pair; the pair itself is never counted. A
/// one-sided entry (file or folder) counts once in <see cref="LeftOnly"/> / <see cref="RightOnly"/> and is not descended.
/// A nested folder pair is identical when nothing below it is a different file or a one-sided entry.
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

    /// <summary>Nested both-sides folder pairs whose own subtree is identical.</summary>
    public int SameFolders { get; set; }

    /// <summary>Nested both-sides folder pairs whose subtree is not identical.</summary>
    public int DifferentFolders { get; set; }

    /// <summary>Different folder pairs with valid modified times where the left is later than the right by more than the tolerance.</summary>
    public int LeftNewerFolders { get; set; }

    public int RightNewerFolders { get; set; }
}

/// <summary>Response of <c>POST /compare/summaries</c>: one summary per request pair, in request order.</summary>
public sealed class CompareSummariesResponse
{
    public List<CompareSummaryDto> Summaries { get; set; } = [];
}
