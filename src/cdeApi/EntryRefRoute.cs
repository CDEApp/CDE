using System;
using cdeAppCore.Dtos;

namespace cdeApi;

/// <summary>
/// Parses the URL form of an entry reference. A <c>ref</c> is <c>{catalogId}-{entryIndex}</c>
/// (both non-negative integers), e.g. <c>0-0</c> is the root of the first catalog.
/// </summary>
public static class EntryRefRoute
{
    public static bool TryParse(string text, out EntryRefDto entryRef)
    {
        entryRef = default;
        if (string.IsNullOrEmpty(text)) return false;

        var dash = text.IndexOf('-');
        if (dash <= 0 || dash == text.Length - 1) return false;

        if (!int.TryParse(text.AsSpan(0, dash), out var catalogId) || catalogId < 0) return false;
        if (!int.TryParse(text.AsSpan(dash + 1), out var entryIndex) || entryIndex < 0) return false;

        entryRef = new EntryRefDto(catalogId, entryIndex);
        return true;
    }
}
