using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>The <c>catalog:</c> modifier of <see cref="EverythingQuery"/>: a term tested against the catalog's file name.</summary>
[TestFixture]
public class EverythingCatalogTests
{
    private static EverythingQuery Q(string query, string catalog)
    {
        var parsed = EverythingQuery.Parse(query);
        parsed.ForCatalog(catalog);
        return parsed;
    }

    [TestCase("catalog:backup", "C-backup-2026.cdex", true)]
    [TestCase("catalog:backup", "C-BACKUP.cdex", true)]
    [TestCase("catalog:backup", "photos.cdex", false)]
    [TestCase("catalog:.cdex", "photos.cdex", true)]
    [TestCase("catalog:*.cdex", "photos.cdex", true)]
    [TestCase("catalog:*.cdex", "photos.cdex.bak", false)]
    [TestCase("catalog:photos*", "photos.cdex", true)]
    [TestCase("catalog:photos*", "old-photos.cdex", false)]
    [TestCase("catalog:p?otos.cdex", "photos.cdex", true)]
    [TestCase("catalog:\"my catalog\"", "my catalog.cdex", true)]
    [TestCase("catalog:\"my catalog\"", "mycatalog.cdex", false)]
    [TestCase("case:catalog:Photos", "Photos.cdex", true)]
    [TestCase("case:catalog:Photos", "photos.cdex", false)]
    [TestCase("regex:catalog:^C-", "C-system.cdex", true)]
    [TestCase("regex:catalog:^C-", "D-system.cdex", false)]
    [TestCase("regex:catalog:\"^(C|D)-\"", "D-system.cdex", true)]
    [TestCase("catalog:<a | b>", "b.cdex", true)]
    [TestCase("catalog:<a | b>", "c.cdex", false)]
    [TestCase("catalog:<a b>", "ab.cdex", true)]
    [TestCase("catalog:<a b>", "a.cdex", false)]
    [TestCase("!catalog:old", "new.cdex", true)]
    [TestCase("!catalog:old", "old.cdex", false)]
    [TestCase("catalog:a | catalog:b", "b.cdex", true)]
    [TestCase("catalog:caf\u00e9", "CAF\u00c9.cdex", true)]
    [TestCase("catalog:x", "", false)]
    public void The_term_is_tested_against_the_catalog_file_name_not_the_entry(string query, string catalog, bool expected)
    {
        // The entry text does not matter: the answer is the same for every entry of the catalog.
        foreach (var entry in new[] { "anything.txt", "zzz", "photos" })
        {
            Assert.That(Q(query, catalog).Matches(entry), Is.EqualTo(expected), "chars " + entry);
            Assert.That(Q(query, catalog).Matches(Encoding.UTF8.GetBytes(entry)), Is.EqualTo(expected), "bytes " + entry);
        }
    }

    [Test]
    public void ForCatalog_can_move_one_query_to_the_next_catalog()
    {
        var query = EverythingQuery.Parse("catalog:photos");
        query.ForCatalog("backup.cdex");
        Assert.That(query.Matches("x"), Is.False);
        query.ForCatalog("photos.cdex");
        Assert.That(query.Matches("x"), Is.True);
        query.ForCatalog(null);
        Assert.That(query.Matches("x"), Is.False);
    }

    [Test]
    public void It_combines_with_terms_that_test_the_entry()
    {
        Assert.That(Q("catalog:backup ext:mp3", "backup.cdex").Matches("a.mp3"), Is.True);
        Assert.That(Q("catalog:backup ext:mp3", "backup.cdex").Matches("a.wav"), Is.False);
        Assert.That(Q("catalog:backup ext:mp3", "photos.cdex").Matches("a.mp3"), Is.False);
        Assert.That(Q("catalog:backup a", "backup.cdex").Matches("a.txt"), Is.True);
        Assert.That(Q("catalog:backup a", "backup.cdex").Matches("b.txt"), Is.False);
        Assert.That(Q("catalog:backup | a", "photos.cdex").Matches("a.txt"), Is.True);
        Assert.That(Q("catalog:backup | a", "photos.cdex").Matches("b.txt"), Is.False);
        Assert.That(Q("!catalog:backup file:", "photos.cdex").Matches("a.txt", isFolder: false), Is.True);
    }

    [Test]
    public void A_catalog_that_cannot_match_is_reported_so_it_can_be_skipped()
    {
        Assert.That(Q("catalog:backup ext:mp3", "photos.cdex").MatchesNothing, Is.True);
        Assert.That(Q("catalog:backup ext:mp3", "backup.cdex").MatchesNothing, Is.False);
        Assert.That(Q("ext:mp3 catalog:backup", "photos.cdex").MatchesNothing, Is.True);
        Assert.That(Q("catalog:backup | ext:mp3", "photos.cdex").MatchesNothing, Is.False);
        Assert.That(Q("catalog:backup | catalog:old", "photos.cdex").MatchesNothing, Is.True);
        Assert.That(Q("!catalog:backup", "backup.cdex").MatchesNothing, Is.True);
        Assert.That(Q("!catalog:backup", "photos.cdex").MatchesNothing, Is.False);
        Assert.That(Q("!catalog:backup ext:mp3", "backup.cdex").MatchesNothing, Is.True);
        Assert.That(Q("ext:mp3", "photos.cdex").MatchesNothing, Is.False);
        Assert.That(Q("", "photos.cdex").MatchesNothing, Is.False);
        Assert.That(Q("catalog:photos", "photos.cdex").MatchesNothing, Is.False);
    }

    [Test]
    public void A_catalog_term_reads_no_text_of_the_entry()
    {
        Assert.That(EverythingQuery.Parse("catalog:x").TextsRead(defaultMatchesPath: true), Is.EqualTo(EverythingTexts.Name));
        Assert.That(EverythingQuery.Parse("catalog:x path:foo").TextsRead(defaultMatchesPath: false), Is.EqualTo(EverythingTexts.Path));
        Assert.That(EverythingQuery.Parse("catalog:x foo").TextsRead(defaultMatchesPath: false), Is.EqualTo(EverythingTexts.Name));
        Assert.That(EverythingQuery.Parse("catalog:x").UsesProperties, Is.False);
    }

    [Test]
    public void The_nearest_target_inside_a_group_wins_without_clashing()
    {
        // path: sits inside the group, so it is the term's own target; there is no catalog: next to it in a chain.
        var entry = Q("catalog:<path:docs>", "x.cdex");
        Assert.That(entry.Matches("a.txt", @"C:\docs\a.txt", defaultMatchesPath: false), Is.True);
        Assert.That(entry.Matches("a.txt", @"C:\other\a.txt", defaultMatchesPath: false), Is.False);
    }

    [TestCase("path:catalog:x", 0)]
    [TestCase("catalog:path:x", 0)]
    [TestCase("no-path:catalog:x", 0)]
    [TestCase("catalog:no-path:x", 0)]
    [TestCase("a regex:path:catalog:x", 8)]
    [TestCase("case:catalog:path:x", 5)]
    [TestCase("catalog:", 0)]
    [TestCase("a catalog: b", 2)]
    [TestCase("catalog:|a", 0)]
    [TestCase("catalog:size:>1", 0)]
    [TestCase("catalog:ext:mp3", 0)]
    public void A_catalog_modifier_that_clashes_or_is_not_attached_is_an_error(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));
        Assert.That(ex!.Position, Is.EqualTo(position), ex.Message);
    }

    [Test]
    public void The_same_target_twice_is_not_a_clash()
    {
        Assert.DoesNotThrow(() => EverythingQuery.Parse("catalog:catalog:x"));
        Assert.DoesNotThrow(() => EverythingQuery.Parse("path:path:x"));
        Assert.DoesNotThrow(() => EverythingQuery.Parse("regex:case:catalog:x"));
        Assert.That(Q("catalog:catalog:x", "x.cdex").Matches("a"), Is.True);
    }

    [Test]
    public void CatalogNames_prefers_the_disk_name_and_falls_back_to_the_stored_one()
    {
        Assert.That(CatalogNames.FileName("photos.cdex", "stale.cde"), Is.EqualTo("photos.cdex"));
        Assert.That(CatalogNames.FileName(null, "stale.cde"), Is.EqualTo("stale.cde"));
        Assert.That(CatalogNames.FileName("", "stale.cde"), Is.EqualTo("stale.cde"));
        Assert.That(CatalogNames.FileName(null, null), Is.EqualTo(string.Empty));
    }
}
