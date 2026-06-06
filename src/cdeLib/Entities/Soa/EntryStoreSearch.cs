using System;
using System.Text;
using System.Text.RegularExpressions;

namespace cdeLib.Entities.Soa;

/// <summary>
/// Index-based find over an <see cref="EntryStore"/> — a linear scan of the flat arrays (cache
/// friendly, no per-entry indirection). Mirrors the core matching of the pointer-tree
/// <c>FindOptions</c> (substring/regex, name/path, file/folder filter) so the two can be proven
/// equivalent before the production search is moved onto the store.
/// </summary>
public static class EntryStoreSearch
{
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

        Regex regex = null;
        if (regexMode && !string.IsNullOrEmpty(pattern))
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        }

        var sb = includePath ? new StringBuilder(260) : null;
        var hasPattern = !string.IsNullOrEmpty(pattern);

        for (var i = 1; i < store.Count; i++) // index 0 is the root, never a result
        {
            var isDir = store.IsDirectory(i);
            if (isDir ? !includeFolders : !includeFiles) continue;

            if (!hasPattern)
            {
                onMatch(i);
                continue;
            }

            bool match;
            if (includePath)
            {
                sb.Clear();
                store.AppendFullPath(sb, i);
                var path = sb.ToString();
                match = regexMode
                    ? regex.IsMatch(path)
                    : path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var name = store.FullName(i);
                match = regexMode
                    ? regex.IsMatch(name)
                    : name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }

            if (match) onMatch(i);
        }
    }
}
