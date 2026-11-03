namespace cdeLib.Hashing;

/// <summary>
/// Which files a hash run hashes. By default every non-empty file whose size another file in the
/// catalogs being hashed shares. A <see cref="Filter"/> narrows that set to the files it matches;
/// <see cref="IncludeUniqueSizes"/> drops the shared-size rule. The selection belongs to one run: it is not saved.
/// Make one with <see cref="Parse"/>, so a held selection always has a valid filter.
/// </summary>
public sealed record HashSelection
{
    public static HashSelection All { get; } = new(null, includeUniqueSizes: false);

    private HashSelection(string filter, bool includeUniqueSizes)
    {
        Filter = filter;
        IncludeUniqueSizes = includeUniqueSizes;
    }

    /// <summary>An Everything-style query (<see cref="EverythingQuery"/>) a file must match, or null for every file.</summary>
    public string Filter { get; }

    /// <summary>
    /// Also hash files whose size no other file has. Each selected file then gets a full hash.
    /// </summary>
    public bool IncludeUniqueSizes { get; }

    /// <summary>A blank <paramref name="filter"/> selects every file.</summary>
    /// <exception cref="EverythingQueryException">The filter is malformed; the message is the one the search bar shows.</exception>
    public static HashSelection Parse(string filter, bool includeUniqueSizes = false)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return includeUniqueSizes ? new HashSelection(null, includeUniqueSizes: true) : All;

        EverythingQuery.Parse(filter);
        return new HashSelection(filter, includeUniqueSizes);
    }
}
