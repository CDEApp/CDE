using System;
using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib;

/// <summary>What <c>dupe:</c> compares. The properties are combined: <c>dupe:size;name</c> needs both to be equal.</summary>
[Flags]
public enum DupeProperties
{
    None = 0,
    Name = 1,
    Path = 2,
    Extension = 4,
    Size = 8,
    DateModified = 16,

    /// <summary>The hash stored in the catalog. A file with no usable full hash has no key and is never a duplicate.</summary>
    Hash = 32,
}

/// <summary>One <c>dupe:</c> function: the properties it compares and whether text is compared with case.</summary>
public readonly record struct DupeSpec(DupeProperties Properties, bool MatchCase);

/// <summary>A 128-bit fingerprint of the values of a <see cref="DupeSpec"/>, so no per-entry key string is kept.</summary>
public readonly record struct DuplicateKey(ulong A, ulong B);

/// <summary>
/// How many entries share each <see cref="DuplicateKey"/>, for every <c>dupe:</c> function of a query, counted over
/// ALL the catalogs given (so a file in two catalogs is a duplicate). Built once per search before any catalog is
/// scanned, then each scan asks <see cref="MaskOf"/> for the entry in hand. It reads only what the catalogs store:
/// a <c>hash</c> property uses the stored hash and never reads a file, and an entry without a usable full hash is left
/// out. The index holds a small entry per distinct key, so a search over a very large catalog uses memory for it.
/// </summary>
public sealed class DuplicateIndex
{
    private readonly Dictionary<DupeSpec, Dictionary<DuplicateKey, int>> _counts;

    private DuplicateIndex(Dictionary<DupeSpec, Dictionary<DuplicateKey, int>> counts) => _counts = counts;

    /// <summary>The most dupe: functions one query may have, because a scan keeps their answers in one 64-bit mask.</summary>
    public const int MaxSpecs = 64;

    /// <summary>Count the keys of <paramref name="specs"/> over every entry (not the root) of every catalog.</summary>
    public static DuplicateIndex Build(IReadOnlyList<DupeSpec> specs, IEnumerable<IEntrySource> catalogs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(catalogs);
        var counts = new Dictionary<DupeSpec, Dictionary<DuplicateKey, int>>();
        foreach (var spec in specs) counts[spec] = [];

        foreach (var catalog in catalogs)
        {
            if (catalog == null) continue;
            foreach (var spec in specs)
            {
                var table = counts[spec];
                for (var i = 1; i < catalog.Count; i++)
                {
                    if (!TryKey(spec, catalog, i, out var key)) continue;
                    table[key] = table.GetValueOrDefault(key) + 1;
                }
            }
        }

        return new DuplicateIndex(counts);
    }

    /// <summary>
    /// The index for the dupe: functions of an Everything <paramref name="pattern"/>, counted over every catalog, or
    /// null when the pattern has none. A malformed pattern throws <see cref="EverythingQueryException"/>.
    /// </summary>
    public static DuplicateIndex ForPattern(string pattern, TimeProvider clock, IEnumerable<IEntrySource> catalogs)
    {
        var query = EverythingQuery.Parse(pattern ?? string.Empty, clock ?? TimeProvider.System);
        return query.DuplicateSpecs.Count == 0 ? null : Build(query.DuplicateSpecs, catalogs);
    }

    /// <summary>
    /// Bit k of the result is set when entry <paramref name="i"/> is a duplicate under <paramref name="specs"/>[k]:
    /// it has a key and at least one other entry has the same key.
    /// </summary>
    public ulong MaskOf(IEntrySource catalog, int i, IReadOnlyList<DupeSpec> specs)
    {
        var mask = 0UL;
        for (var k = 0; k < specs.Count; k++)
        {
            if (!_counts.TryGetValue(specs[k], out var table)) continue;
            if (TryKey(specs[k], catalog, i, out var key) && table.GetValueOrDefault(key) > 1) mask |= 1UL << k;
        }

        return mask;
    }

    // False when the entry has no key: it is left out of the counts and is never a duplicate.
    private static bool TryKey(DupeSpec spec, IEntrySource catalog, int i, out DuplicateKey key)
    {
        var p = spec.Properties;
        var builder = new KeyBuilder();
        if ((p & DupeProperties.Hash) != 0)
        {
            // Only a full hash that is stored in the catalog; a partial hash does not prove equal content.
            if (!catalog.HasHash || !catalog.IsHashDone(i) || catalog.IsPartialHash(i))
            {
                key = default;
                return false;
            }

            var hash = catalog.HashOf(i);
            builder.Add(hash.HashA);
            builder.Add(hash.HashB);
        }

        if ((p & DupeProperties.Size) != 0) builder.Add((ulong)catalog.SizeOf(i));
        if ((p & DupeProperties.DateModified) != 0) builder.Add((ulong)catalog.ModifiedOf(i).Ticks);
        if ((p & DupeProperties.Name) != 0) builder.Add(catalog.FullName(i), spec.MatchCase);
        if ((p & DupeProperties.Extension) != 0) builder.Add(ExtensionOf(catalog.FullName(i)), spec.MatchCase);
        if ((p & DupeProperties.Path) != 0) builder.Add(catalog.FullPath(i), spec.MatchCase);

        key = builder.Key;
        return true;
    }

    private static string ExtensionOf(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : string.Empty;
    }

    // Two independent 64-bit lanes: a collision needs both to collide.
    private struct KeyBuilder
    {
        private ulong _a = 0x9E3779B97F4A7C15UL;
        private ulong _b = 0xC2B2AE3D27D4EB4FUL;

        public KeyBuilder()
        {
        }

        public readonly DuplicateKey Key => new(_a, _b);

        public void Add(ulong value)
        {
            _a = (_a ^ value) * 0x100000001B3UL;
            _a ^= _a >> 29;
            _b = (_b + value) * 0xFF51AFD7ED558CCDUL;
            _b ^= _b >> 32;
        }

        public void Add(string text, bool matchCase)
        {
            foreach (var c in text) Add(matchCase ? c : char.ToUpperInvariant(c));
            Add(ulong.MaxValue); // ends the text, so a|bc and ab|c differ
        }
    }
}
