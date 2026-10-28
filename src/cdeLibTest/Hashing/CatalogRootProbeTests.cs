using System;
using System.Diagnostics;
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
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// <see cref="CatalogRootProbe"/>: before hashing, each catalog's root is checked once (in parallel, with a time
/// limit) and catalogs whose drive or share can't be reached are skipped, instead of failing every file in them.
/// </summary>
[TestFixture]
public class CatalogRootProbeTests
{
    private string _dir;

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-root-probe").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    // A catalog of <folder> (one file), written as <name>.cdex; returns the catalog path.
    private string Catalog(string folder, string name)
    {
        var root = Path.Combine(_dir, folder);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "a.txt"), "a");
        var re = new RootEntry();
        re.PopulateRoot(root);
        var cdex = Path.Combine(_dir, name + ".cdex");
        ColumnarFormat.Write(re, cdex);
        return cdex;
    }

    [Test]
    public async Task Catalogs_with_a_reachable_root_are_kept_in_order()
    {
        var a = Catalog("a", "one");
        var b = Catalog("b", "two");

        var check = await CatalogRootProbe.CheckAsync([b, a], TimeSpan.FromSeconds(10));

        Assert.That(check.Reachable, Is.EqualTo(new[] { b, a }));
        Assert.That(check.Unreachable, Is.Empty);
    }

    [Test]
    public async Task A_catalog_whose_root_is_gone_is_skipped()
    {
        var kept = Catalog("a", "one");
        var gone = Catalog("b", "two");
        Directory.Delete(Path.Combine(_dir, "b"), recursive: true);

        var check = await CatalogRootProbe.CheckAsync([kept, gone], TimeSpan.FromSeconds(10));

        Assert.That(check.Reachable, Is.EqualTo(new[] { kept }));
        var skipped = check.Unreachable[0];
        Assert.That(skipped.CatalogPath, Is.EqualTo(gone));
        Assert.That(skipped.RootPath, Does.StartWith(Path.Combine(_dir, "b")));
        Assert.That(skipped.Reason, Does.Contain("not found"));
    }

    [Test]
    public async Task A_root_that_does_not_answer_in_time_is_skipped_without_waiting_for_it()
    {
        var slow = Catalog("a", "one");
        using var release = new ManualResetEventSlim();
        var sw = Stopwatch.StartNew();

        var check = await CatalogRootProbe.CheckAsync([slow], TimeSpan.FromMilliseconds(200), _ =>
        {
            release.Wait(TimeSpan.FromSeconds(30)); // an open on an unreachable share blocks like this
            return true;
        });

        sw.Stop();
        release.Set();
        Assert.That(check.Reachable, Is.Empty);
        Assert.That(check.Unreachable[0].Reason, Does.Contain("no answer"));
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public async Task Each_distinct_root_is_checked_once()
    {
        var first = Catalog("a", "one");
        var second = Catalog("a", "two"); // same root
        var checks = 0;

        var check = await CatalogRootProbe.CheckAsync([first, second], TimeSpan.FromSeconds(10), root =>
        {
            Interlocked.Increment(ref checks);
            return Directory.Exists(root);
        });

        Assert.That(checks, Is.EqualTo(1));
        Assert.That(check.Reachable, Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public async Task An_unreadable_catalog_is_passed_on_so_hashing_reports_it_as_before()
    {
        var broken = Path.Combine(_dir, "broken.cdex");
        File.WriteAllBytes(broken, [1, 2, 3]);

        var check = await CatalogRootProbe.CheckAsync([broken], TimeSpan.FromSeconds(10));

        Assert.That(check.Reachable, Is.EqualTo(new[] { broken }));
    }

    [Test]
    public async Task Hashing_a_catalog_whose_root_is_unreachable_leaves_it_untouched()
    {
        var cdex = Catalog("gone", "offline");
        Directory.Delete(Path.Combine(_dir, "gone"), recursive: true);
        var before = File.ReadAllBytes(cdex);
        var logger = Substitute.For<Serilog.ILogger>();
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(1024);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        var handler = new HashCatalogCommandHandler(logger, Substitute.For<IApplicationDiagnostics>(),
            new Duplication(Substitute.For<ILogger>(), config, Substitute.For<IApplicationDiagnostics>(),
                new OperationCancellation()),
            new CatalogRepository(Serilog.Core.Logger.None));

        await handler.OnHandle(new HashCatalogCommand { CatalogPath = cdex }, CancellationToken.None);

        Assert.That(File.ReadAllBytes(cdex), Is.EqualTo(before));
        Assert.That(File.Exists(cdex + ".tmp"), Is.False);
        Assert.That(logger.ReceivedCalls(), Has.Some.Matches<NSubstitute.Core.ICall>(c =>
            c.GetMethodInfo().Name == "Warning" && c.GetArguments()[0] is string t && t.Contains("Skipping")));
    }
}
