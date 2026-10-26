namespace cdeAppCore.Dtos;

/// <summary>
/// Progress for a catalog (re)load: <see cref="Current"/> of <see cref="Total"/> catalogs loaded,
/// plus a human-readable <see cref="Message"/>. Reported via <see cref="System.IProgress{T}"/> so any
/// frontend (or the API's SSE stream) can surface loading progress without a UI dependency.
/// </summary>
public readonly record struct CatalogLoadProgress(int Current, int Total, string Message);
