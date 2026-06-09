using System;
using System.Collections.Generic;
using System.Linq;
using cdeAppCore.Dtos;

namespace cdeApi;

/// <summary>
/// Optional ordering for a directory listing, driven by the <c>?sort</c> query parameter:
/// <c>name</c> | <c>size</c> | <c>date</c>, optionally prefixed with <c>-</c> for descending.
/// Directories sort before files on each key (matching the WinForms listing). When no sort is
/// supplied the natural (catalog) order is preserved.
/// </summary>
public static class DirectoryNodeSort
{
    public static IReadOnlyList<DirectoryNodeDto> Apply(IReadOnlyList<DirectoryNodeDto> nodes, string sort)
    {
        if (string.IsNullOrEmpty(sort)) return nodes;

        var descending = sort[0] == '-';
        var key = descending ? sort[1..] : sort;

        IOrderedEnumerable<DirectoryNodeDto> ordered = key switch
        {
            "name" => nodes.OrderBy(n => n.IsDirectory ? 0 : 1).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),
            "size" => nodes.OrderBy(n => n.IsDirectory ? 0 : 1).ThenBy(n => n.Size),
            "date" => nodes.OrderBy(n => n.IsDirectory ? 0 : 1).ThenBy(n => n.Modified),
            _ => null
        };

        if (ordered is null) return nodes;
        return descending ? ordered.Reverse().ToList() : ordered.ToList();
    }
}
