using System;
using System.Collections.Generic;
using System.Threading;
using cdeAppCore.Dtos;
using cdeLib;

namespace cdeAppCore.Search;

/// <summary>
/// The single streaming search primitive shared by every frontend. Matching rows are yielded as they
/// are found (not only at the end), progress is reported on a throttled cadence, and cancellation
/// stops the underlying traversal promptly. <c>cdeWin</c> consumes the stream in-process with
/// <c>await foreach</c>; <c>cdeApi</c> maps the same stream to Server-Sent Events.
/// </summary>
public interface ISearchService
{
    IAsyncEnumerable<SearchResultRow> SearchAsync(
        SearchQuery query,
        IProgress<SearchProgress> progress = null,
        CancellationToken cancellationToken = default);
}
