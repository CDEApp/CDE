using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using NSubstitute;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

[TestFixture]
[NonParallelizable]
public class HashSelectionTests
{
    private const int FirstPass = 1024;
    private const long GiB = 1L << 30;

    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cdehash230-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private sealed class RecordingHashHelper(ILogger logger, Action<string> onRead = null) : HashHelper(logger)
    {
        public ConcurrentBag<string> Read { get; } = [];

        public override ValueTask<HashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
        {
            Read.Add(Path.GetFileName(filename));
            onRead?.Invoke(filename);
            return base.GetHashResponseFromFile(filename, bytesToHash);
        }
    }

    private static Duplication NewDuplication(HashHelper helper = null)
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(FirstPass);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        var logger = Substitute.For<ILogger>();
        return new Duplication(logger, config, Substitute.For<IApplicationDiagnostics>(), new OperationCancellation(),
            helper ?? new HashHelper(logger));
    }

    private static byte[] Bytes(int seed)
    {
        var b = new byte[4096];
        new Random(seed).NextBytes(b);
        return b;
    }

    private static readonly Dictionary<string, (long StatedSize, int ContentSeed)> StatedSizes = new()
    {
        ["big1.bin"] = (3 * GiB, 1),
        ["big2.bin"] = (3 * GiB, 1),
        ["diffA.bin"] = (4 * GiB, 2),
        ["diffB.bin"] = (4 * GiB, 3),
        ["small1.bin"] = (1 * GiB, 4),
        ["small2.bin"] = (1 * GiB, 4),
        ["lone.bin"] = (5 * GiB, 5),
    };

    private string CatalogWithStatedSizes(string name = "root") => Catalog(name, StatedSizes);

    // Each file holds 4 KB of seeded bytes (so a first-pass hash covers only part of it) but the catalog states the given size.
    private string Catalog(string name, IReadOnlyDictionary<string, (long StatedSize, int ContentSeed)> files)
    {
        var root = Path.Combine(_dir, name);
        Directory.CreateDirectory(root);
        foreach (var (file, (_, seed)) in files) File.WriteAllBytes(Path.Combine(root, file), Bytes(seed));

        var config = Substitute.For<IConfiguration>();
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        var tree = new RootEntry(config);
        tree.PopulateRoot(root);
        foreach (var entry in tree.Children) entry.Size = files[entry.Path].StatedSize;

        var cdex = Path.Combine(_dir, name + ".cdex");
        ColumnarFormat.Write(tree, cdex);
        return cdex;
    }

    private static Dictionary<string, (long Size, EFlags Flags)> Entries(string cdex)
    {
        using var r = new ColumnarCatalogReader(cdex);
        return Enumerable.Range(1, r.Count - 1)
            .Where(i => !r.IsDirectory(i))
            .ToDictionary(i => Path.GetFileName(r.FullPath(i)), i => (r.SizeOf(i), r.Flags(i)));
    }

    private static Hash16 HashOf(string cdex, string fileName)
    {
        using var r = new ColumnarCatalogReader(cdex);
        var i = Enumerable.Range(1, r.Count - 1).Single(i => Path.GetFileName(r.FullPath(i)) == fileName);
        return r.HashOf(i);
    }

    private static bool IsFullHash(EFlags f) => f.IsHashUsable() && (f & EFlags.PartialHash) == 0;

    [Test]
    public async Task A_size_filter_hashes_every_matching_candidate_and_no_file_below_it()
    {
        var cdex = CatalogWithStatedSizes();

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null,
            selection: HashSelection.Parse("size:>2gb"));

        var entries = Entries(cdex);
        var repeated = entries.Values.GroupBy(e => e.Size).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        Assert.Multiple(() =>
        {
            foreach (var (name, (size, flags)) in entries)
            {
                if (size < 2 * GiB)
                    Assert.That(flags & EFlags.HashDone, Is.EqualTo(EFlags.None), $"{name} is below 2 GB");
                else if (repeated.Contains(size))
                    Assert.That(flags.IsHashUsable(), Is.True, $"{name} matches and shares its size");
            }

            Assert.That(IsFullHash(entries["big1.bin"].Flags), Is.True, "a duplicate pair is promoted to a full hash");
            Assert.That(entries["lone.bin"].Flags & EFlags.HashDone, Is.EqualTo(EFlags.None), "a unique size is not hashed");
        });
    }

    [Test]
    public async Task Including_unique_sizes_gives_every_selected_file_a_full_hash()
    {
        var cdex = CatalogWithStatedSizes();

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null,
            selection: HashSelection.Parse("size:>2gb", includeUniqueSizes: true));

        var entries = Entries(cdex);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { "big1.bin", "big2.bin", "diffA.bin", "diffB.bin", "lone.bin" })
                Assert.That(IsFullHash(entries[name].Flags), Is.True, name);
            Assert.That(entries["small1.bin"].Flags & EFlags.HashDone, Is.EqualTo(EFlags.None));
        });
    }

    [Test]
    public void An_invalid_filter_fails_with_the_search_bar_message()
    {
        const string query = "size:>2gb \"open";
        var search = Assert.Throws<EverythingQueryException>(() => EverythingQuery.Parse(query));

        var hash = Assert.Throws<EverythingQueryException>(() => HashSelection.Parse(query));

        Assert.That(hash!.Message, Is.EqualTo(search!.Message));
    }

    [Test]
    public void A_blank_filter_selects_every_file()
    {
        Assert.That(HashSelection.Parse("  "), Is.SameAs(HashSelection.All));
    }

    [Test]
    public async Task A_cancelled_run_leaves_the_catalog_byte_identical()
    {
        var cdex = CatalogWithStatedSizes();
        var original = File.ReadAllBytes(cdex);
        using var cts = new CancellationTokenSource();
        var helper = new RecordingHashHelper(Substitute.For<ILogger>(), _ => cts.Cancel());

        await Assert.CatchAsync<OperationCanceledException>(() =>
            ColumnarCatalogHashing.HashAndSave(NewDuplication(helper), [cdex], null, cancellationToken: cts.Token));

        Assert.That(helper.Read, Is.Not.Empty, "the run was cancelled while it hashed");
        Assert.That(File.ReadAllBytes(cdex), Is.EqualTo(original));
        Assert.That(File.Exists(cdex + ".tmp"), Is.False);
    }

    [Test]
    public async Task A_replace_callback_puts_each_hashed_copy_in_place_instead_of_the_move()
    {
        var first = CatalogWithStatedSizes("one");
        var second = CatalogWithStatedSizes("two");
        var original = File.ReadAllBytes(first);
        var replaced = new List<(string Staged, string Catalog)>();

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [first, second], null, replace: (staged, catalog) =>
        {
            replaced.Add((staged, catalog));
            File.Move(staged, catalog + ".hashed");
        });

        Assert.Multiple(() =>
        {
            Assert.That(replaced, Is.EqualTo(new[] { (first + ".tmp", first), (second + ".tmp", second) }));
            Assert.That(File.ReadAllBytes(first), Is.EqualTo(original), "the run itself moved nothing over the catalog");
            Assert.That(Entries(first + ".hashed")["big1.bin"].Flags.IsHashUsable(), Is.True);
        });
    }

    [Test]
    public async Task A_replace_that_fails_on_one_catalog_still_saves_the_others_and_leaves_no_temp_file()
    {
        var first = CatalogWithStatedSizes("one");
        var second = CatalogWithStatedSizes("two");
        var original = File.ReadAllBytes(first);

        var ex = await Assert.ThrowsAsync<HashFailedException>(() =>
            ColumnarCatalogHashing.HashAndSave(NewDuplication(), [first, second], null, replace: (staged, catalog) =>
            {
                if (catalog == first) throw new IOException("in use");
                File.Move(staged, catalog, overwrite: true);
            }));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("in use"));
            Assert.That(File.ReadAllBytes(first), Is.EqualTo(original));
            Assert.That(Entries(second)["big1.bin"].Flags.IsHashUsable(), Is.True);
            Assert.That(File.Exists(first + ".tmp") || File.Exists(second + ".tmp"), Is.False);
        });
    }

    [Test]
    public async Task Progress_counts_the_files_and_bytes_of_the_current_pass_up_to_its_totals()
    {
        var root = Path.Combine(_dir, "sized");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "same1.bin"), Bytes(1));
        File.WriteAllBytes(Path.Combine(root, "same2.bin"), Bytes(1));
        File.WriteAllBytes(Path.Combine(root, "other.bin"), Bytes(2));
        var tree = new RootEntry(Substitute.For<IConfiguration>());
        tree.PopulateRoot(root);
        var cdex = Path.Combine(_dir, "sized.cdex");
        ColumnarFormat.Write(tree, cdex);
        HashRunProgress? endOfFirstPass = null;
        var duplication = NewDuplication();
        duplication.StatusMessageEvent = message =>
        {
            if (message == "After initial partial hashing phase.") endOfFirstPass = duplication.Progress;
        };

        await ColumnarCatalogHashing.HashAndSave(duplication, [cdex], null);

        Assert.Multiple(() =>
        {
            Assert.That(endOfFirstPass, Is.EqualTo(new HashRunProgress(false, 3, 3, 3 * FirstPass, 3 * FirstPass)));
            Assert.That(duplication.Progress, Is.EqualTo(new HashRunProgress(true, 2, 2, 2 * 4096, 2 * 4096)));
        });
    }

    [Test]
    public async Task Cancelling_while_saving_leaves_every_catalog_byte_identical()
    {
        var first = CatalogWithStatedSizes("one");
        var second = CatalogWithStatedSizes("two");
        var originals = new[] { File.ReadAllBytes(first), File.ReadAllBytes(second) };
        using var cts = new CancellationTokenSource();

        await Assert.CatchAsync<OperationCanceledException>(() =>
            ColumnarCatalogHashing.HashAndSave(NewDuplication(), [first, second],
                file => { if (file == second) cts.Cancel(); }, cancellationToken: cts.Token));

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(first), Is.EqualTo(originals[0]), "the catalog saved before the cancel");
            Assert.That(File.ReadAllBytes(second), Is.EqualTo(originals[1]));
            Assert.That(File.Exists(first + ".tmp") || File.Exists(second + ".tmp"), Is.False, "no temp file is left");
        });
    }

    [Test]
    public async Task A_copy_that_only_has_a_partial_hash_is_promoted_when_a_same_size_file_has_a_full_hash()
    {
        var cdex = Catalog("mixed", new Dictionary<string, (long, int)>
        {
            ["disk.iso"] = (3 * GiB, 7),
            ["disk.bin"] = (3 * GiB, 7),
        });
        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null,
            selection: HashSelection.Parse("ext:iso", includeUniqueSizes: true));
        Assert.That(IsFullHash(Entries(cdex)["disk.iso"].Flags), Is.True);
        Assert.That(Entries(cdex)["disk.bin"].Flags & EFlags.HashDone, Is.EqualTo(EFlags.None));

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null);

        Assert.That(IsFullHash(Entries(cdex)["disk.bin"].Flags), Is.True, "a partial hash cannot be compared with a full one");
        Assert.That(HashOf(cdex, "disk.bin"), Is.EqualTo(HashOf(cdex, "disk.iso")), "so cde dupes sees the pair");
    }

    [Test]
    public async Task A_second_run_reads_only_the_files_without_a_hash()
    {
        var cdex = CatalogWithStatedSizes();
        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null,
            selection: HashSelection.Parse("size:>2gb"));

        var helper = new RecordingHashHelper(Substitute.For<ILogger>());
        await ColumnarCatalogHashing.HashAndSave(NewDuplication(helper), [cdex], null);

        Assert.That(helper.Read.Distinct(), Is.EquivalentTo(new[] { "small1.bin", "small2.bin" }));
    }

    [Test]
    public async Task A_filter_keeps_the_hashes_of_files_it_does_not_match()
    {
        var cdex = CatalogWithStatedSizes();
        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null);
        var hashed = Entries(cdex);

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [cdex], null,
            selection: HashSelection.Parse("size:>2gb"));

        Assert.That(Entries(cdex)["small1.bin"].Flags, Is.EqualTo(hashed["small1.bin"].Flags));
        Assert.That(IsFullHash(hashed["small1.bin"].Flags), Is.True);
    }

    [Test]
    public async Task The_preview_counts_the_files_the_run_hashes()
    {
        var cdex = CatalogWithStatedSizes();
        var selection = HashSelection.Parse("size:>2gb");
        HashPreview before;
        using (var reader = new ColumnarCatalogReader(cdex)) before = ColumnarCatalogHashing.Preview([reader], selection);

        var helper = new RecordingHashHelper(Substitute.For<ILogger>());
        await ColumnarCatalogHashing.HashAndSave(NewDuplication(helper), [cdex], null, selection: selection);

        HashPreview after;
        using (var reader = new ColumnarCatalogReader(cdex)) after = ColumnarCatalogHashing.Preview([reader], selection);
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo(new HashPreview(4, 14 * GiB, 0)));
            Assert.That(helper.Read.Distinct().Count(), Is.EqualTo(before.Files));
            Assert.That(after, Is.EqualTo(new HashPreview(0, 0, 4)));
        });
    }
}
