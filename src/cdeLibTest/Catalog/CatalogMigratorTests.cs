using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NUnit.Framework;
using Serilog.Core;

namespace cdeLibTest.Catalog;

/// <summary>
/// <see cref="CatalogMigrator"/> converts a legacy .cde to a .cdex beside it, writing through a .tmp, and renames the
/// source to .cde.migrated. It never overwrites a .cdex or a .cde.migrated.
/// </summary>
[TestFixture]
public class CatalogMigratorTests
{
    private string _dir;
    private CatalogRepository _repository;
    private CatalogMigrator _migrator;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-migrator-test").FullName;
        _repository = new CatalogRepository(Logger.None);
        _migrator = new CatalogMigrator(_repository);
    }

    [TearDown]
    public void TearDown()
    {
        _repository.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private string Cde => Path.Combine(_dir, "one.cde");
    private string Cdex => Path.Combine(_dir, "one.cdex");
    private string Migrated => Path.Combine(_dir, "one.cde.migrated");

    // data/ a.txt, sub/ b.txt  ->  one.cde
    private async Task<RootEntry> WriteLegacyCatalogAsync()
    {
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "sub"));
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        File.WriteAllText(Path.Combine(data, "sub", "b.txt"), "bb");
        var re = new RootEntry();
        re.PopulateRoot(data);
        re.ActualFileName = Cde;
        await _repository.Save(re);
        return re;
    }

    private string[] LeftoverTmpFiles() => Directory.GetFiles(_dir, "*.tmp");

    [Test]
    public async Task Migrate_writes_the_cdex_and_renames_the_source()
    {
        var re = await WriteLegacyCatalogAsync();

        var result = _migrator.Migrate(Cde);

        Assert.That(result.Outcome, Is.EqualTo(MigrationOutcome.Migrated), result.Message);
        Assert.That(result.TargetPath, Is.EqualTo(Cdex));
        Assert.That(result.Entries, Is.GreaterThan(0));
        using (var reader = new ColumnarCatalogReader(Cdex))
        {
            Assert.That(reader.RootPath, Is.EqualTo(re.Path));
            Assert.That(reader.Count, Is.EqualTo(result.Entries));
        }
        Assert.That(File.Exists(Cde), Is.False);
        Assert.That(File.Exists(Migrated), Is.True);
        Assert.That(LeftoverTmpFiles(), Is.Empty);
    }

    [Test]
    public async Task Migrate_skips_a_cde_that_already_has_a_cdex()
    {
        await WriteLegacyCatalogAsync();
        File.WriteAllText(Cdex, "existing");

        var result = _migrator.Migrate(Cde);

        Assert.That(result.Outcome, Is.EqualTo(MigrationOutcome.Skipped));
        Assert.That(File.ReadAllText(Cdex), Is.EqualTo("existing"));
        Assert.That(File.Exists(Cde), Is.True);
        Assert.That(File.Exists(Migrated), Is.False);
    }

    [Test]
    public void Migrate_an_unreadable_cde_fails_and_leaves_no_output()
    {
        File.WriteAllBytes(Cde, [1, 2, 3, 4, 5]);

        var result = _migrator.Migrate(Cde);

        Assert.That(result.Outcome, Is.EqualTo(MigrationOutcome.Failed));
        Assert.That(result.Message, Is.Not.Empty);
        Assert.That(File.Exists(Cdex), Is.False);
        Assert.That(LeftoverTmpFiles(), Is.Empty);
        Assert.That(File.Exists(Cde), Is.True);
    }

    [Test]
    public async Task Migrate_keeps_the_source_when_the_migrated_name_is_taken()
    {
        await WriteLegacyCatalogAsync();
        File.WriteAllText(Migrated, "older copy");

        var result = _migrator.Migrate(Cde);

        Assert.That(result.Outcome, Is.EqualTo(MigrationOutcome.MigratedNotRenamed));
        Assert.That(result.Message, Is.Not.Empty);
        Assert.That(File.Exists(Cdex), Is.True);
        Assert.That(File.Exists(Cde), Is.True);
        Assert.That(File.ReadAllText(Migrated), Is.EqualTo("older copy"));
    }

    [Test]
    public async Task Migrate_cancelled_writes_nothing()
    {
        await WriteLegacyCatalogAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => _migrator.Migrate(Cde, cts.Token));

        Assert.That(File.Exists(Cdex), Is.False);
        Assert.That(LeftoverTmpFiles(), Is.Empty);
        Assert.That(File.Exists(Cde), Is.True);
    }

    [Test]
    public void Unmigrated_keeps_only_cde_files_without_a_cdex_beside_them()
    {
        var withTwin = Path.Combine(_dir, "done.cde");
        var without = Path.Combine(_dir, "todo.cde");
        File.WriteAllText(withTwin, "");
        File.WriteAllText(Path.Combine(_dir, "done.cdex"), "");
        File.WriteAllText(without, "");

        Assert.That(CatalogMigrator.Unmigrated([withTwin, without]), Is.EqualTo(new[] { without }));
    }
}
