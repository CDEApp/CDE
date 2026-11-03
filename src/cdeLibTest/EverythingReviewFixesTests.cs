using System;
using System.Collections.Generic;
using System.Threading;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>Regression tests for the review of the Everything functions PR (#223).</summary>
[TestFixture]
public class EverythingReviewFixesTests
{
    [Test]
    public void A_case_sensitive_regex_and_an_ordinary_pattern_that_looks_like_its_cache_key_do_not_share_a_regex()
    {
        // A case-sensitive "foo" was cached under "s:foo", the same key as the ordinary pattern "s:foo".
        Assert.That(EverythingQuery.Parse("case:regex:foo").Matches("FOO"), Is.False);
        Assert.That(EverythingQuery.Parse("regex:s:foo").Matches("S:FOO"), Is.True);
        Assert.That(EverythingQuery.Parse("regex:s:foo").Matches("S:FOO"), Is.True);
        Assert.That(EverythingQuery.Parse("case:regex:s:foo").Matches("S:FOO"), Is.False);
        Assert.That(EverythingQuery.Parse("case:regex:foo").Matches("foo"), Is.True);
    }

    [TestCase("<empty:>", true)]
    [TestCase("<ext:>", true)]
    [TestCase("<dupe:>", true)]
    [TestCase("<case:ext:>", true)]
    [TestCase("a <empty:> | b", true)]
    [TestCase("!<empty:>", true)]
    [TestCase("<size:>1> ", true)]
    [TestCase("<size:>=1>", true)]
    [TestCase("<child-count:>1>", true)]
    [TestCase("<dm:<2000>", true)]
    public void A_function_with_no_numeric_value_can_end_a_group(string query, bool parses)
    {
        if (parses) Assert.DoesNotThrow(() => EverythingQuery.Parse(query));
    }

    [Test]
    public void A_group_around_empty_matches_like_empty_does()
    {
        var group = EverythingQuery.Parse("<empty:>");
        Assert.That(group.Matches("x", true, counts: default), Is.True);
        Assert.That(group.Matches("x", true, counts: new EntryCounts(1, 0, 1, 0)), Is.False);
        Assert.That(EverythingQuery.Parse("<ext:>").Matches("README", false), Is.True);
        Assert.That(EverythingQuery.Parse("<ext:>").Matches("a.txt", false), Is.False);
    }

    [Test]
    public void An_unbalanced_group_around_a_function_is_still_an_error()
    {
        Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("<empty:"));
        Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse("empty:>"));
    }

    [Test]
    public void Counting_duplicates_stops_when_the_search_is_cancelled()
    {
        var root = new RootEntry { Path = @"C:\", VolumeName = "V", DefaultFileName = "v.cde", DriveLetterHint = "C" };
        root.AddChild(new DirEntry(false) { Path = "a.txt", Size = 1 });
        root.AddChild(new DirEntry(false) { Path = "b.txt", Size = 1 });
        root.SetInMemoryFields();
        var store = EntryStore.Build(root);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            DuplicateIndex.ForPattern("dupe:size", null, [store], cts.Token));
        Assert.That(DuplicateIndex.ForPattern("dupe:size", null, [store]), Is.Not.Null);
    }

    [Test]
    public void Names_that_differ_only_in_the_case_of_a_letter_stored_as_a_surrogate_pair_are_duplicates()
    {
        // Deseret capital long I (U+10400) and its small letter (U+10428) are each two UTF-16 units.
        var root = new RootEntry { Path = @"C:\", VolumeName = "V", DefaultFileName = "v.cde", DriveLetterHint = "C" };
        root.AddChild(new DirEntry(false) { Path = "\U00010400x.txt", Size = 1 });
        root.AddChild(new DirEntry(false) { Path = "\U00010428X.TXT", Size = 2 });
        root.AddChild(new DirEntry(false) { Path = "other.txt", Size = 3 });
        root.SetInMemoryFields();
        var store = EntryStore.Build(root);

        var hits = new List<string>();
        EntryStoreSearch.Find(store, new EntryStoreFindOptions { Pattern = "dupe:name", EverythingMode = true },
            i => hits.Add(store.FullName(i)));
        Assert.That(hits, Is.EquivalentTo(new[] { "\U00010400x.txt", "\U00010428X.TXT" }));

        hits.Clear();
        EntryStoreSearch.Find(store, new EntryStoreFindOptions { Pattern = "case:dupe:name", EverythingMode = true },
            i => hits.Add(store.FullName(i)));
        Assert.That(hits, Is.Empty);
    }

    [Test]
    public void EntryCountTable_ignores_a_parent_outside_the_catalog()
    {
        var table = EntryCountTable.FromParents([-1, 5, 0], [true, true, false], descendants: true);
        Assert.That(table.Of(0), Is.EqualTo(new EntryCounts(1, 0, 1, 0)));
        Assert.That(table.Of(1), Is.EqualTo(new EntryCounts(0, 0, 0, 0)));
    }

    [Test]
    public void EntryCountTable_does_not_count_through_a_root_that_is_its_own_parent()
    {
        // Entry 1 comes before its parent 2, so the walk up from it must stop at the root, whose parent link is 0.
        var table = EntryCountTable.FromParents([0, 2, 0], [true, false, true], descendants: true);
        Assert.That(table.Of(0), Is.EqualTo(new EntryCounts(0, 1, 1, 1)));
        Assert.That(table.Of(2), Is.EqualTo(new EntryCounts(1, 0, 1, 0)));
    }
}
