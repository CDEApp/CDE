using System;

namespace cdeLib;

/// <summary>The text of an Everything-style query is not valid. <see cref="Position"/> is the 0-based character offset.</summary>
public sealed class EverythingQueryException(string reason, int position)
    : FormatException($"{reason} (at position {position})")
{
    public int Position { get; } = position;
}

/// <summary>
/// The text of one entry for an <see cref="EverythingQuery"/>, as UTF-8. <see cref="Path"/> is only read when the
/// query needs it, so an implementation can build it lazily. Implementations are classes (the query keeps no copy).
/// </summary>
public interface IEntryText
{
    /// <summary>The entry name (name plus extension).</summary>
    ReadOnlySpan<byte> Name { get; }

    /// <summary>The full path, including the name.</summary>
    ReadOnlySpan<byte> Path { get; }

    /// <summary>True for a folder, false for a file. Only read when the query has a file: or folder: term.</summary>
    bool IsFolder { get; }

    /// <summary>The size in bytes. Only read when the query has a size: term, so override it to support one.</summary>
    long Size => 0;

    /// <summary>The modified time as local ticks. Only read when the query has a date-modified: term.</summary>
    long ModifiedTicks => 0;

    /// <summary>The child and descendant counts. Only read when the query has a count function or empty:.</summary>
    EntryCounts Counts => default;
}

/// <summary>Same as <see cref="IEntryText"/> but as characters.</summary>
public interface IEntryChars
{
    ReadOnlySpan<char> Name { get; }
    ReadOnlySpan<char> Path { get; }
    bool IsFolder { get; }

    /// <summary>See <see cref="IEntryText.Size"/>.</summary>
    long Size => 0;

    /// <summary>See <see cref="IEntryText.ModifiedTicks"/>.</summary>
    long ModifiedTicks => 0;

    /// <summary>See <see cref="IEntryText.Counts"/>.</summary>
    EntryCounts Counts => default;
}

/// <summary>Which of an entry's texts an <see cref="EverythingQuery"/> reads.</summary>
public enum EverythingTexts
{
    /// <summary>Only the name (also the answer for an empty query).</summary>
    Name,

    /// <summary>Only the full path.</summary>
    Path,

    /// <summary>Both: some terms read the name and others the path.</summary>
    Both,
}
