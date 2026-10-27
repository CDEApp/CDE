using System;
using System.Buffers;
using cdeLib.Infrastructure;

namespace cdeLib.Entities.Soa;

/// <summary>
/// Index-based find over an <see cref="EntryStore"/> — a linear scan of the flat arrays (cache
/// friendly, no per-entry indirection). Mirrors the core matching of the pointer-tree
/// <c>FindOptions</c> (substring/regex, name/path, file/folder filter) so the two can be proven
/// equivalent before the production search is moved onto the store.
/// </summary>
public static class EntryStoreSearch
{
    /// <summary>
    /// Full-filter search (pattern + name/path + file/folder + size/date/hour ranges), mirroring the
    /// cdeWin GUI search, evaluated directly against the store arrays.
    /// </summary>
    /// <param name="isCancelled">Polled every 4096 entries; return true to stop early (GUI cancel).</param>
    /// <param name="onScan">Called every 4096 entries with the running scanned count (GUI progress).</param>
    public static void Find(EntryStore store, EntryStoreFindOptions o, Action<int> onMatch,
        Func<bool> isCancelled = null, Action<int> onScan = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(o);
        if (!o.IncludeFiles && !o.IncludeFolders) return;

        // Non-null exactly when regex mode applies; the match site tests it (not RegexMode) so the flow is
        // provably non-null. Bounded: a pathological pattern times out per entry (no match) and ends the scan.
        BoundedRegex regex = null;
        if (o.RegexMode && !string.IsNullOrEmpty(o.Pattern))
            regex = new BoundedRegex(o.Pattern);

        var hasPattern = !string.IsNullOrEmpty(o.Pattern);

        // Substring path matching runs Span<char>.Contains over a rented buffer; only regex mode
        // (rarer) materialises a string. Avoids a full-path string allocation per scanned entry.
        var pathBuffer = o.IncludePath ? ArrayPool<char>.Shared.Rent(512) : null;
        var nameBuffer = o.IncludePath ? null : ArrayPool<char>.Shared.Rent(256);
        try
        {
            for (var i = 1; i < store.Count; i++)
            {
                if ((i & 4095) == 0)
                {
                    if (isCancelled != null && isCancelled()) return;
                    onScan?.Invoke(i);
                }
                if (regex is { Exhausted: true }) return; // pattern timed out repeatedly; abandon the scan

                var isDir = store.IsDirectory(i);
                if (isDir ? !o.IncludeFolders : !o.IncludeFiles) continue;

                var size = store.Size[i];
                if (o.FromSizeEnable && size < o.FromSize) continue;
                if (o.ToSizeEnable && size > o.ToSize) continue;

                if (o.FromDateEnable || o.ToDateEnable || o.FromHourEnable || o.ToHourEnable || o.NotOlderThanEnable)
                {
                    var modified = store.Modified(i);
                    if (o.FromDateEnable && modified < o.FromDate) continue;
                    if (o.ToDateEnable && modified > o.ToDate) continue;
                    if (o.NotOlderThanEnable && modified < o.NotOlderThan) continue;
                    var tod = modified.TimeOfDay;
                    if (o.FromHourEnable && tod < o.FromHour) continue;
                    if (o.ToHourEnable && tod > o.ToHour) continue;
                }

                if (!hasPattern) { onMatch(i); continue; }

                bool match;
                if (o.IncludePath)
                {
                    var path = WritePath(store, i, ref pathBuffer);
                    match = regex is not null
                        ? regex.IsMatch(path)
                        : path.Contains(o.Pattern, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    var name = FullNameSpan(store, i, ref nameBuffer);
                    match = regex is not null
                        ? regex.IsMatch(name)
                        : name.Contains(o.Pattern, StringComparison.OrdinalIgnoreCase);
                }

                if (match) onMatch(i);
            }
        }
        finally
        {
            regex?.ReportIfExhausted(); // never truncate silently
            if (pathBuffer != null) ArrayPool<char>.Shared.Return(pathBuffer);
            if (nameBuffer != null) ArrayPool<char>.Shared.Return(nameBuffer);
        }
    }

    /// <summary>
    /// Entry <paramref name="i"/>'s full name (name + extension) as a span: the stored name itself when
    /// there is no extension, otherwise the two parts joined in the rented <paramref name="buffer"/>
    /// (grown if needed). Avoids the string a rejoin would allocate.
    /// </summary>
    private static ReadOnlySpan<char> FullNameSpan(EntryStore store, int i, ref char[] buffer)
    {
        var name = store.Name[i];
        var ext = store.Ext[i];
        if (string.IsNullOrEmpty(ext)) return name;
        var len = (name?.Length ?? 0) + ext.Length;
        if (len > buffer.Length)
        {
            ArrayPool<char>.Shared.Return(buffer);
            buffer = ArrayPool<char>.Shared.Rent(len);
        }
        name.AsSpan().CopyTo(buffer);
        ext.AsSpan().CopyTo(buffer.AsSpan(name?.Length ?? 0));
        return buffer.AsSpan(0, len);
    }

    /// <summary>
    /// Write entry <paramref name="i"/>'s full path into <paramref name="buffer"/> (rented), growing
    /// and re-renting if it doesn't fit, and return the written span. The grown buffer is passed back
    /// via <paramref name="buffer"/> so the caller reuses it for subsequent entries.
    /// </summary>
    private static ReadOnlySpan<char> WritePath(EntryStore store, int i, ref char[] buffer)
    {
        int len;
        while ((len = store.TryWriteFullPath(buffer, i)) < 0)
        {
            var bigger = ArrayPool<char>.Shared.Rent(buffer.Length * 2);
            ArrayPool<char>.Shared.Return(buffer);
            buffer = bigger;
        }

        return buffer.AsSpan(0, len);
    }

    /// <summary>Invoke <paramref name="onMatch"/> with the index of every entry matching the query.</summary>
    public static void Find(
        EntryStore store,
        string pattern,
        bool regexMode,
        bool includePath,
        bool includeFiles,
        bool includeFolders,
        Action<int> onMatch)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(onMatch);

        if (!includeFiles && !includeFolders) return;

        BoundedRegex regex = null; // non-null exactly when regex mode applies (see Find)
        if (regexMode && !string.IsNullOrEmpty(pattern))
        {
            regex = new BoundedRegex(pattern);
        }

        var hasPattern = !string.IsNullOrEmpty(pattern);

        var pathBuffer = includePath ? ArrayPool<char>.Shared.Rent(512) : null;
        var nameBuffer = includePath ? null : ArrayPool<char>.Shared.Rent(256);
        try
        {
            for (var i = 1; i < store.Count; i++) // index 0 is the root, never a result
            {
                var isDir = store.IsDirectory(i);
                if (isDir ? !includeFolders : !includeFiles) continue;
                if (regex is { Exhausted: true }) return; // pattern timed out repeatedly; abandon the scan

                if (!hasPattern)
                {
                    onMatch(i);
                    continue;
                }

                bool match;
                if (includePath)
                {
                    var path = WritePath(store, i, ref pathBuffer);
                    match = regex is not null
                        ? regex.IsMatch(path)
                        : path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    var name = FullNameSpan(store, i, ref nameBuffer);
                    match = regex is not null
                        ? regex.IsMatch(name)
                        : name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                }

                if (match) onMatch(i);
            }
        }
        finally
        {
            regex?.ReportIfExhausted(); // never truncate silently
            if (pathBuffer != null) ArrayPool<char>.Shared.Return(pathBuffer);
            if (nameBuffer != null) ArrayPool<char>.Shared.Return(nameBuffer);
        }
    }
}
