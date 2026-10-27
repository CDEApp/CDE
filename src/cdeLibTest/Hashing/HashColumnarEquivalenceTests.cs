using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using NSubstitute;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #40: <c>cde hash</c> over memory-mapped readers must leave the catalogs with exactly the hashes and flags
/// the tree-based path produced (rebuild the tree, <see cref="Duplication.ApplyHash(IList{RootEntry})"/>, write the
/// tree back). The catalogs are scanned from real files on disk so both paths really hash.
/// </summary>
[TestFixture]
[NonParallelizable]
public class HashColumnarEquivalenceTests
{
    private const int FirstPass = 1024;

    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cdehash40-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(FirstPass);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        return config;
    }

    private static Duplication NewDuplication(OperationCancellation cancellation = null) =>
        new(Substitute.For<ILogger>(), Config(), Substitute.For<IApplicationDiagnostics>(),
            cancellation ?? new OperationCancellation());

    private static byte[] Bytes(int size, int seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    private string Put(string relative, byte[] content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string Scan(string folder, Action<RootEntry> before = null)
    {
        var root = new RootEntry(Config());
        root.PopulateRoot(Path.Combine(_dir, folder));
        before?.Invoke(root);
        var cdex = Path.Combine(_dir, folder + ".cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    /// <summary>
    /// Two catalogs with every hashing case: identical files bigger than the first pass (promoted to full),
    /// same-size files differing in the first block (stay partial), files equal in the first block but not after
    /// it (full hashes differ), small identical files (full hash in the first pass), unique and zero sizes, a
    /// cross-catalog duplicate, a file that vanished after the scan, an existing full hash and a legacy partial.
    /// </summary>
    private List<string> BuildCatalogs()
    {
        var big = Bytes(4096, 1);
        Put(@"one\dupA.bin", big);
        Put(@"one\sub\dupB.bin", big);
        Put(@"one\diff1.bin", Bytes(3000, 2));
        Put(@"one\diff2.bin", Bytes(3000, 3));
        var prefix = Bytes(5000, 4);
        var prefix2 = (byte[])prefix.Clone();
        prefix2[^1] ^= 0xFF;
        Put(@"one\prefix1.bin", prefix);
        Put(@"one\sub\prefix2.bin", prefix2);
        var small = Bytes(500, 5);
        Put(@"one\small1.bin", small);
        Put(@"one\small2.bin", small);
        Put(@"one\unique.bin", Bytes(777, 6));
        Put(@"one\empty1.bin", []);
        Put(@"one\empty2.bin", []);
        var gone = Put(@"one\gone.bin", Bytes(6000, 7));
        Put(@"one\keep.bin", Bytes(6000, 8));

        Put(@"two\dupC.bin", big);
        var fullBefore = Put(@"two\full1.bin", Bytes(7000, 9));
        Put(@"two\full2.bin", File.ReadAllBytes(fullBefore));
        Put(@"two\legacy1.bin", Bytes(8000, 10));
        Put(@"two\legacy2.bin", Bytes(8000, 11));

        var one = Scan("one");
        File.Delete(gone);
        var two = Scan("two", root =>
        {
            var full = root.Children.First(c => c.Path == "full1.bin");
            var response = new HashHelper(Substitute.For<ILogger>()).GetHashResponseFromFile(fullBefore, null)
                .AsTask().GetAwaiter().GetResult();
            full.SetHash(response.Hash);
            full.IsPartialHash = false;
            var legacy = root.Children.First(c => c.Path == "legacy1.bin");
            legacy.BitFields |= EFlags.HashDone | EFlags.PartialHash; // pre-#37 partial: no V2 bit
            legacy.Hash = new Hash16(0xBAD, 0);
        });
        return [one, two];
    }

    private static List<byte[]> HashWithTrees(IList<string> files)
    {
        var trees = CatalogTreeBuilder.FromColumnarFiles(files);
        NewDuplication().ApplyHash(trees).GetAwaiter().GetResult();
        var result = new List<byte[]>();
        foreach (var tree in trees)
        {
            var path = tree.ActualFileName + ".tree";
            ColumnarFormat.Write(EntryStore.Build(tree), path);
            result.Add(File.ReadAllBytes(path));
        }

        return result;
    }

    private static async Task<List<byte[]>> HashWithReaders(IList<string> files, Duplication duplication = null)
    {
        var readers = files.Select(f => new ColumnarCatalogReader(f)).ToList();
        try
        {
            var updates = await (duplication ?? NewDuplication()).ApplyHash(readers);
            var result = new List<byte[]>();
            for (var c = 0; c < readers.Count; c++)
            {
                var path = files[c] + ".columnar";
                ColumnarFormat.WriteWithHashes(readers[c], updates.For(c), files[c], path);
                result.Add(File.ReadAllBytes(path));
            }

            return result;
        }
        finally
        {
            foreach (var r in readers) r.Dispose();
        }
    }

    private static Dictionary<string, (EFlags Flags, Hash16 Hash)> Entries(byte[] catalog)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, catalog);
            using var r = new ColumnarCatalogReader(path);
            return Enumerable.Range(1, r.Count - 1).ToDictionary(r.FullPath, i => (r.Flags(i), r.HashOf(i)));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task Readers_produce_the_same_catalogs_as_the_tree_path()
    {
        var files = BuildCatalogs();
        var expected = HashWithTrees(files);
        var actual = await HashWithReaders(files);

        // guard against a vacuous comparison: the fixture must exercise full, partial and failed hashing.
        var one = Entries(expected[0]);
        var two = Entries(expected[1]);
        EFlags F(Dictionary<string, (EFlags Flags, Hash16 Hash)> e, string name) =>
            e.Single(kv => kv.Key.EndsWith(name, StringComparison.Ordinal)).Value.Flags;
        Assert.Multiple(() =>
        {
            Assert.That(F(one, "dupA.bin") & (EFlags.HashDone | EFlags.PartialHash), Is.EqualTo(EFlags.HashDone));
            Assert.That(F(one, "diff1.bin") & EFlags.PartialHash, Is.EqualTo(EFlags.PartialHash));
            Assert.That(F(one, "unique.bin") & EFlags.HashDone, Is.EqualTo(EFlags.None));
            Assert.That(F(one, "keep.bin") & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
            Assert.That(F(two, "legacy1.bin") & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
            Assert.That(F(two, "dupC.bin") & (EFlags.HashDone | EFlags.PartialHash), Is.EqualTo(EFlags.HashDone));
        });

        for (var c = 0; c < files.Count; c++)
        {
            Assert.That(Entries(actual[c]), Is.EquivalentTo(Entries(expected[c])), $"flags and hashes of {files[c]}");
            Assert.That(actual[c], Is.EqualTo(expected[c]), $"bytes of {files[c]}");
        }
    }

    [Test]
    public async Task Hashing_in_place_replaces_each_catalog_with_its_hashed_copy()
    {
        var files = BuildCatalogs();
        var expected = HashWithTrees(files);
        var saved = new List<string>();

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), files, saved.Add);

        Assert.That(saved, Is.EqualTo(files));
        for (var c = 0; c < files.Count; c++)
        {
            Assert.That(File.ReadAllBytes(files[c]), Is.EqualTo(expected[c]));
            Assert.That(File.Exists(files[c] + ".tmp"), Is.False);
        }
    }

    [Test]
    public async Task A_catalog_that_cannot_be_saved_does_not_stop_the_others_being_saved()
    {
        var files = BuildCatalogs();
        var expected = HashWithTrees(files);
        var original = File.ReadAllBytes(files[0]);
        File.SetAttributes(files[0], FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => ColumnarCatalogHashing.HashAndSave(NewDuplication(), files, null));

            Assert.That(File.ReadAllBytes(files[0]), Is.EqualTo(original), "the unsaved catalog is left as it was");
            Assert.That(File.Exists(files[0] + ".tmp"), Is.False);
            Assert.That(File.ReadAllBytes(files[1]), Is.EqualTo(expected[1]), "the other catalog is still saved");
        }
        finally { File.SetAttributes(files[0], FileAttributes.Normal); }
    }

    [Test]
    public async Task A_second_run_over_hashed_catalogs_changes_nothing()
    {
        var files = BuildCatalogs();
        var first = await HashWithReaders(files);
        for (var c = 0; c < files.Count; c++) File.WriteAllBytes(files[c], first[c]);

        var second = await HashWithReaders(files);
        for (var c = 0; c < files.Count; c++) Assert.That(second[c], Is.EqualTo(first[c]));
    }

    [Test]
    public async Task Break_during_partial_hashing_still_returns_the_hashes_computed_so_far()
    {
        var files = BuildCatalogs();
        var cancellation = new OperationCancellation();
        cancellation.Cancel(); // the break key was pressed: the partial phase stops after its first file

        var cancelled = await HashWithReaders(files, NewDuplication(cancellation));
        var complete = HashWithTrees(files);

        var hashed = cancelled.SelectMany(Entries).Where(kv => kv.Value.Flags.IsHashUsable()).ToList();
        var allHashed = complete.SelectMany(Entries).Count(kv => kv.Value.Flags.IsHashUsable());
        Assert.That(hashed, Is.Not.Empty, "hashes computed before the break are kept");
        Assert.That(hashed.Count, Is.LessThan(allHashed), "the break stopped the run early");
    }
}
