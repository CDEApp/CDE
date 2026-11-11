using System;

namespace cdeLib;

/// <summary>
/// Progress for a search/find operation: <see cref="Count"/> entries visited of <see cref="Total"/>,
/// with wall-clock <see cref="Elapsed"/> since the search started. A plain value type with no UI
/// coupling, reported via <see cref="IProgress{T}"/> so any frontend (or none) can consume it.
/// <see cref="LimitReached"/> is set on the final report only, when the result limit stopped the search and at least
/// one further match exists.
/// </summary>
public readonly record struct SearchProgress(int Count, int Total, TimeSpan Elapsed, bool LimitReached = false);
