namespace cdeAppCore.Dtos;

/// <summary>
/// A serializable reference to a single catalog entry: the index of the catalog in the loaded
/// session (<see cref="CatalogId"/>) plus the entry index within that catalog's
/// <c>IEntrySource</c> (<see cref="EntryIndex"/>). This is the single addressing scheme used by
/// tree nodes, directory rows, and search results, and is what crosses the API boundary so the
/// host can resolve a real filesystem path (never a client-supplied one) for shell actions.
/// </summary>
public readonly record struct EntryRefDto(int CatalogId, int EntryIndex);
