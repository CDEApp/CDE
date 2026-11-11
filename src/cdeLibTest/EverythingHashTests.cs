using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class EverythingHashTests
{
    private static readonly Hash16 Same = new(0x0123456789abcdefUL, 0xfedcba9876543210UL);
    private const string SameHex = "0123456789abcdeffedcba9876543210";

    [Test]
    public void Hex_is_the_xxh128sum_form_and_parses_back_in_either_case()
    {
        var value = ((UInt128)0x0123456789abcdefUL << 64) | 0xfedcba9876543210UL;

        Assert.That(new Hash16(value).ToHex(), Is.EqualTo(SameHex));
        Assert.That(Hash16.TryParseHex(SameHex.ToUpperInvariant(), out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(Same));
    }

    [Test]
    public void Hash_matches_only_an_entry_with_that_full_hash()
    {
        var query = EverythingQuery.Parse("hash:" + SameHex.ToUpperInvariant());

        Assert.That(query.UsesFullHash, Is.True);
        Assert.That(query.Matches("a.bin", fullHash: Same), Is.True);
        Assert.That(query.Matches("a.bin", fullHash: new Hash16(1UL, 2UL)), Is.False);
        Assert.That(query.Matches("a.bin"), Is.False, "no usable full hash");
        Assert.That(EverythingQuery.Parse("a.bin").UsesFullHash, Is.False);
    }

    [TestCase("hash:", 0)]
    [TestCase("hash:0123", 0)]
    [TestCase("x hash:0123456789abcdeffedcba98765432100", 2)]
    [TestCase("hash:0123456789abcdeffedcba987654321g", 0)]
    [TestCase("hash:+123456789abcdeffedcba9876543210", 0)]
    public void Hash_needs_exactly_32_hex_digits(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex.Position, Is.EqualTo(position));
        Assert.That(ex.Message, Does.Contain("32 hex digits"));
    }

    [Test]
    public void Case_cannot_modify_hash()
    {
        Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("case:hash:" + SameHex));
    }

    private static RootEntry Tree()
    {
        var root = new RootEntry { Path = @"C:\data", VolumeName = "DATA", DefaultFileName = "DATA.cde", DriveLetterHint = "C" };
        var folder = new DirEntry(true) { Path = "folder" };
        root.AddChild(folder);
        foreach (var (name, hash, partial) in new (string, Hash16?, bool)[]
                 {
                     ("same.bin", Same, false), ("other.bin", new Hash16(1UL, 2UL), false),
                     ("partial.bin", Same, true), ("unhashed.bin", null, false),
                 })
        {
            var file = new DirEntry(false) { Path = name, Size = 10 };
            if (hash is { } h)
            {
                file.SetHash(h);
                file.IsPartialHash = partial;
            }

            root.AddChild(file);
        }

        var copy = new DirEntry(false) { Path = "copy.bin", Size = 10 };
        copy.SetHash(Same);
        folder.AddChild(copy);
        root.SetInMemoryFields();
        return root;
    }

    private static List<string> Find(IEntrySource source, string pattern)
    {
        var names = new List<string>();
        source.Find(new EntryStoreFindOptions { Pattern = pattern, EverythingMode = true }, i => names.Add(source.FullName(i)));
        return names;
    }

    [TestCase("hash:" + SameHex, new[] { "same.bin", "copy.bin" })]
    [TestCase("hash:" + SameHex + " path:folder", new[] { "copy.bin" })]
    [TestCase("hash:" + SameHex + " | unhashed", new[] { "same.bin", "copy.bin", "unhashed.bin" })]
    [TestCase("!hash:" + SameHex + " file:", new[] { "other.bin", "partial.bin", "unhashed.bin" })]
    public void Finds_the_file_and_its_copies_in_a_store_and_a_cdex(string pattern, string[] expected)
    {
        var store = EntryStore.Build(Tree());
        Assert.That(Find(store, pattern), Is.EquivalentTo(expected), "EntryStore");

        var file = Path.Combine(Path.GetTempPath(), $"cdelibtest-hash-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(store, file);
            using var reader = new ColumnarCatalogReader(file);
            Assert.That(Find(reader, pattern), Is.EquivalentTo(expected), ".cdex");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void A_count_of_same_hash_covers_every_catalog_and_is_zero_without_a_full_hash()
    {
        var one = EntryStore.Build(Tree());
        var two = EntryStore.Build(Tree());
        var index = DuplicateIndex.Build([DupeSpec.SameHash], [one, two]);

        int Count(string name) => index.CountOf(DupeSpec.SameHash, one,
            Enumerable.Range(1, one.Count - 1).Single(i => one.FullName(i) == name));

        Assert.That(Count("same.bin"), Is.EqualTo(4), "same.bin and copy.bin in both catalogs");
        Assert.That(Count("other.bin"), Is.EqualTo(2));
        Assert.That(Count("partial.bin"), Is.Zero);
        Assert.That(Count("unhashed.bin"), Is.Zero);
        Assert.That(Count("folder"), Is.Zero);
    }
}
