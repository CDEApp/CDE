using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// <c>cde hash &lt;catalog&gt;</c>: <see cref="HashCatalogTargets"/> picks the catalogs, and a run over one catalog
/// hashes and saves only that one. Hashing compares sizes within the catalogs being hashed, so a file whose only
/// same-size match is in another catalog is left for a full <c>cde hash</c>.
/// </summary>
[TestFixture]
public class HashSingleCatalogTests
{
    private string _dir;

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-hash-single").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    // ---- choosing the catalogs ----

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void No_catalog_given_means_every_catalog_found(string catalogPath)
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetColumnarFileList(Arg.Any<IEnumerable<string>>()).Returns(new List<string> { "a.cdex", "b.cdex" });

        var files = HashCatalogTargets.Resolve(catalogPath, repository, out var problem);

        Assert.That(files, Is.EqualTo(new[] { "a.cdex", "b.cdex" }));
        Assert.That(problem, Is.Null);
    }

    [Test]
    public void No_catalogs_found_explains_how_to_get_some()
    {
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetColumnarFileList(Arg.Any<IEnumerable<string>>()).Returns(new List<string>());

        var files = HashCatalogTargets.Resolve(null, repository, out var problem);

        Assert.That(files, Is.Empty);
        Assert.That(problem, Does.Contain("cde migrate"));
    }

    [Test]
    public void A_named_cdex_is_the_only_catalog_and_nothing_else_is_searched()
    {
        var cdex = Touch("one.cdex");
        var repository = Substitute.For<ICatalogRepository>();

        var files = HashCatalogTargets.Resolve(cdex, repository, out var problem);

        Assert.That(files, Is.EqualTo(new[] { cdex }));
        Assert.That(problem, Is.Null);
        repository.DidNotReceiveWithAnyArgs().GetColumnarFileList(default!);
    }

    [Test]
    public void A_missing_catalog_is_reported()
    {
        var missing = Path.Combine(_dir, "nope.cdex");

        var files = HashCatalogTargets.Resolve(missing, Substitute.For<ICatalogRepository>(), out var problem);

        Assert.That(files, Is.Empty);
        Assert.That(problem, Does.Contain("not found").And.Contain(missing));
    }

    [Test]
    public void A_legacy_cde_is_refused_with_the_migrate_command_to_run()
    {
        var cde = Touch("old.cde");

        var files = HashCatalogTargets.Resolve(cde, Substitute.For<ICatalogRepository>(), out var problem);

        Assert.That(files, Is.Empty);
        Assert.That(problem, Does.Contain($"cde migrate {cde}"));
    }

    [Test]
    public void Another_kind_of_file_is_refused()
    {
        var txt = Touch("notes.txt");

        var files = HashCatalogTargets.Resolve(txt, Substitute.For<ICatalogRepository>(), out var problem);

        Assert.That(files, Is.Empty);
        Assert.That(problem, Does.Contain("not a .cdex catalog"));
    }

    // ---- hashing one catalog ----

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(1024);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        return config;
    }

    private static byte[] Bytes(int size, int seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    private void Put(string relative, byte[] content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private string Scan(string folder)
    {
        var root = new RootEntry(Config());
        root.PopulateRoot(Path.Combine(_dir, folder));
        var cdex = Path.Combine(_dir, folder + ".cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    private static EFlags FlagsOf(string cdex, string fileName)
    {
        using var reader = new ColumnarCatalogReader(cdex);
        for (var i = 1; i < reader.Count; i++)
        {
            if (reader.FullPath(i).EndsWith(fileName, StringComparison.Ordinal)) return reader.Flags(i);
        }

        throw new InvalidOperationException($"{fileName} is not in {cdex}");
    }

    [Test]
    public async Task Hashing_one_catalog_hashes_and_saves_only_that_catalog()
    {
        var dup = Bytes(4096, 1);
        Put(@"one\dupA.bin", dup);
        Put(@"one\dupB.bin", dup);
        var shared = Bytes(3333, 2);
        Put(@"one\acrossOne.bin", shared); // its only same-size match is in the other catalog
        Put(@"two\acrossTwo.bin", shared);
        Put(@"two\dupC.bin", dup);
        Put(@"two\dupD.bin", dup);
        var one = Scan("one");
        var two = Scan("two");
        var twoBefore = File.ReadAllBytes(two);
        var handler = new HashCatalogCommandHandler(Substitute.For<Serilog.ILogger>(),
            Substitute.For<IApplicationDiagnostics>(),
            new Duplication(Substitute.For<ILogger>(), Config(), Substitute.For<IApplicationDiagnostics>(),
                new OperationCancellation()),
            new CatalogRepository(Serilog.Core.Logger.None));

        await handler.OnHandle(new HashCatalogCommand { CatalogPath = one }, CancellationToken.None);

        Assert.That(FlagsOf(one, "dupA.bin") & (EFlags.HashDone | EFlags.PartialHash), Is.EqualTo(EFlags.HashDone));
        Assert.That(FlagsOf(one, "acrossOne.bin") & EFlags.HashDone, Is.EqualTo(EFlags.None));
        Assert.That(File.ReadAllBytes(two), Is.EqualTo(twoBefore));
    }
}
