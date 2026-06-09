using System;

namespace cdeLib;

/// <summary>
/// Progress for a search/find operation: <see cref="Count"/> entries visited of <see cref="Total"/>,
/// with wall-clock <see cref="Elapsed"/> since the search started. A plain value type with no UI
/// coupling, reported via <see cref="IProgress{T}"/> so any frontend (or none) can consume it.
/// </summary>
public readonly record struct SearchProgress(int Count, int Total, TimeSpan Elapsed);
