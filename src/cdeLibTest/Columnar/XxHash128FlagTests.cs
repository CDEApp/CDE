using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using cdeLib.Output;
using NSubstitute;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Columnar;

[TestFixture]
[NonParallelizable]
public class XxHash128FlagTests
{
    private const int FirstPass = 1024;

    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cdehash222-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "data"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [TestCase(false, false, CatalogHashes.None)]
    [TestCase(false, true, CatalogHashes.None)]
    [TestCase(true, false, CatalogHashes.XxHash128)]
    [TestCase(true, true, CatalogHashes.XxHash128)]
    public void ComposeFlags_round_trips_through_ParseHashKind(bool hasHashes, bool followJunctions, CatalogHashes expected)
    {
        var flags = ColumnarFormat.ComposeFlags(hasHashes, followJunctions);

        Assert.That(ColumnarFormat.ParseHashKind(flags), Is.EqualTo(expected));
        Assert.That((flags & ColumnarFormat.FlagFollowJunctions) != 0, Is.EqualTo(followJunctions));
    }

    [TestCase(0, CatalogHashes.None)]
    [TestCase(ColumnarFormat.FlagFollowJunctions, CatalogHashes.None)]
    [TestCase(ColumnarFormat.FlagHasHashes, CatalogHashes.Legacy)]
    [TestCase(ColumnarFormat.FlagHasHashes | ColumnarFormat.FlagFollowJunctions, CatalogHashes.Legacy)]
    [TestCase(ColumnarFormat.FlagHashXxHash128, CatalogHashes.None)]
    [TestCase(ColumnarFormat.FlagHasHashes | ColumnarFormat.FlagHashXxHash128, CatalogHashes.XxHash128)]
    public void ParseHashKind_reads_headers_of_older_builds(int headerFlags, CatalogHashes expected) =>
        Assert.That(ColumnarFormat.ParseHashKind(headerFlags), Is.EqualTo(expected));

    [Test]
    public void Reader_of_a_catalog_written_by_this_build_trusts_its_hashes()
    {
        using var r = new ColumnarCatalogReader(WriteTree(Tree(withHashes: true)));

        Assert.That(r.Hashes, Is.EqualTo(CatalogHashes.XxHash128));
        Assert.That(r.HasHashes, Is.True);
        Assert.That(r.IsHashDone(Index(r, "full.bin")), Is.True);
        Assert.That(r.HashOf(Index(r, "full.bin")), Is.EqualTo(new Hash16(1, 2)));
        Assert.That(r.IsPartialHash(Index(r, "partial.bin")), Is.True);
    }

    [Test]
    public void Reader_of_a_legacy_catalog_shows_every_entry_as_unhashed_and_keeps_the_other_flags()
    {
        var path = WriteTree(Tree(withHashes: true));
        LegacyCatalog.MarkPre222(path);
        using var r = new ColumnarCatalogReader(path);

        Assert.That(r.Hashes, Is.EqualTo(CatalogHashes.Legacy));
        Assert.That(r.HasHashes, Is.False);
        var full = Index(r, "full.bin");
        var partial = Index(r, "partial.bin");
        Assert.That(r.IsHashDone(full), Is.False);
        Assert.That(r.IsHashDone(partial), Is.False);
        Assert.That(r.IsPartialHash(partial), Is.False);
        Assert.That(r.HashOf(full), Is.EqualTo(default(Hash16)));
        var bits = r.BitFields;
        Assert.That(bits[full] & FlagsExtensions.HashBits, Is.EqualTo(EFlags.None));
        Assert.That(bits[partial] & FlagsExtensions.HashBits, Is.EqualTo(EFlags.None));
        Assert.That(bits[Index(r, "link")], Is.EqualTo(EFlags.Directory | EFlags.ReparsePoint));
        Assert.That(r.FlagsOf(Index(r, "link")), Is.EqualTo(EFlags.Directory | EFlags.ReparsePoint));
    }

    [Test]
    public void Reader_of_an_unhashed_catalog_reports_None()
    {
        using var r = new ColumnarCatalogReader(WriteTree(Tree(withHashes: false)));

        Assert.That(r.Hashes, Is.EqualTo(CatalogHashes.None));
        Assert.That(r.HasHashes, Is.False);
    }

    [Test]
    public async Task Hash_of_a_legacy_catalog_recomputes_every_candidate_and_flags_the_result()
    {
        Put("a1.bin", Bytes(3000, 1));
        Put("a2.bin", Bytes(3000, 2));
        var same = Bytes(500, 3);
        Put("b1.bin", same);
        Put("b2.bin", same);
        Put("unique.bin", Bytes(777, 4));
        var cdex = Scan(root =>
        {
            foreach (var f in root.Children)
            {
                f.SetHash(new Hash16(0xBAD, 0xBAD));
                f.IsPartialHash = false;
            }
        });
        LegacyCatalog.MarkPre222(cdex);
        var logger = Substitute.For<ILogger>();

        await HashAndSave(cdex, logger);

        using var r = new ColumnarCatalogReader(cdex);
        Assert.That(r.Hashes, Is.EqualTo(CatalogHashes.XxHash128));
        foreach (var name in new[] { "a1.bin", "a2.bin" })
        {
            var i = Index(r, name);
            Assert.That(r.IsHashDone(i), Is.True, name);
            Assert.That(r.IsPartialHash(i), Is.True, name);
            Assert.That(r.HashOf(i), Is.EqualTo(Expected(FileBytes(name).AsSpan(0, FirstPass))), name);
        }

        foreach (var name in new[] { "b1.bin", "b2.bin" })
        {
            var i = Index(r, name);
            Assert.That(r.IsHashDone(i), Is.True, name);
            Assert.That(r.IsPartialHash(i), Is.False, name);
            Assert.That(r.HashOf(i), Is.EqualTo(Expected(FileBytes(name))), name);
        }

        Assert.That(r.IsHashDone(Index(r, "unique.bin")), Is.False, "not a candidate, and its legacy hash is not carried forward");
        Assert.That(r.HashOf(Index(r, "unique.bin")), Is.EqualTo(default(Hash16)));
        logger.Received(1).LogInfo(Arg.Is<string>(f => f.Contains("older cde version") && f.Contains(cdex)), Arg.Any<object[]>());
    }

    [Test]
    public async Task Hash_of_a_legacy_catalog_shows_the_older_version_line_through_the_status_event()
    {
        var same = Bytes(500, 3);
        Put("b1.bin", same);
        Put("b2.bin", same);
        var cdex = Scan();
        var logger = Substitute.For<ILogger>();
        var shown = new List<string>();

        await HashAndSave(cdex, logger, shown.Add);
        Assert.That(shown, Has.None.Contains("older cde version"), "control: a catalog written by this build");

        LegacyCatalog.MarkPre222(cdex);
        await HashAndSave(cdex, logger, shown.Add);

        Assert.That(shown, Has.One.Contains("older cde version").And.One.Contains(cdex));
        logger.DidNotReceive().LogInfo(Arg.Is<string>(f => f.Contains("older cde version")), Arg.Any<object[]>());
    }

    [Test]
    public async Task Hash_of_a_catalog_written_by_this_build_keeps_its_hashes_and_does_not_warn()
    {
        var same = Bytes(500, 3);
        Put("b1.bin", same);
        Put("b2.bin", same);
        var kept = new Hash16(7, 8);
        var cdex = Scan(root =>
        {
            foreach (var f in root.Children)
            {
                f.SetHash(kept);
                f.IsPartialHash = false;
            }
        });
        var logger = Substitute.For<ILogger>();

        await HashAndSave(cdex, logger);

        using var r = new ColumnarCatalogReader(cdex);
        Assert.That(r.HashOf(Index(r, "b1.bin")), Is.EqualTo(kept));
        logger.DidNotReceive().LogInfo(Arg.Is<string>(f => f.Contains("older cde version")), Arg.Any<object[]>());
    }

    [Test]
    public void Rescan_reuse_copies_nothing_from_a_legacy_catalog()
    {
        var path = WriteTree(Tree(withHashes: true));

        var control = Tree(withHashes: false);
        using (var r = new ColumnarCatalogReader(path)) ColumnarHashReuse.CopyHashes(r, control);
        Assert.That(File_(control, "full.bin").IsHashDone, Is.True, "control: the flagged catalog is reused");

        LegacyCatalog.MarkPre222(path);
        var fresh = Tree(withHashes: false);
        using (var r = new ColumnarCatalogReader(path)) ColumnarHashReuse.CopyHashes(r, fresh);

        foreach (var name in new[] { "full.bin", "partial.bin", "plain.bin" })
        {
            Assert.That(File_(fresh, name).IsHashDone, Is.False, name);
            Assert.That(File_(fresh, name).Hash, Is.EqualTo(default(Hash16)), name);
        }
    }

    [Test]
    public async Task Loading_a_cde_drops_its_hashes_and_keeps_the_other_flags()
    {
        var cde = Path.Combine(_dir, "t.cde");
        var root = Tree(withHashes: true);
        root.ActualFileName = cde;
        using var repository = new CatalogRepository(Serilog.Core.Logger.None);
        await repository.Save(root);

        var loaded = repository.LoadDirCache(cde);

        foreach (var name in new[] { "full.bin", "partial.bin", "plain.bin" })
        {
            var e = File_(loaded, name);
            Assert.That(e.IsHashDone, Is.False, name);
            Assert.That(e.BitFields & FlagsExtensions.HashBits, Is.EqualTo(EFlags.None), name);
            Assert.That(e.Hash, Is.EqualTo(default(Hash16)), name);
        }

        Assert.That(File_(loaded, "link").BitFields, Is.EqualTo(EFlags.Directory | EFlags.ReparsePoint));
    }

    [Test]
    public async Task Dupes_over_a_legacy_catalog_finds_no_sets()
    {
        var same = Bytes(2000, 5);
        Put("d1.bin", same);
        Put("d2.bin", same);
        var helper = new HashHelper(Substitute.For<ILogger>());
        var hash = (await helper.GetHashResponseFromFile(Path.Combine(_dir, "data", "d1.bin"), null)).Hash;
        var cdex = Scan(root =>
        {
            foreach (var f in root.Children)
            {
                f.SetHash(hash);
                f.IsPartialHash = false;
            }
        });
        var logger = Substitute.For<ILogger>();

        using (var r = new ColumnarCatalogReader(cdex))
            Assert.That(new ColumnarDuplicateFinder(logger).GetDuplicateSets([r]), Has.Count.EqualTo(1), "control");

        LegacyCatalog.MarkPre222(cdex);
        using (var r = new ColumnarCatalogReader(cdex))
            Assert.That(new ColumnarDuplicateFinder(logger).GetDuplicateSets([r]), Is.Empty);
    }

    [Test]
    public async Task The_dupes_command_warns_about_a_legacy_catalog_and_not_about_a_flagged_one()
    {
        var same = Bytes(2000, 5);
        Put("d1.bin", same);
        Put("d2.bin", same);
        var helper = new HashHelper(Substitute.For<ILogger>());
        var hash = (await helper.GetHashResponseFromFile(Path.Combine(_dir, "data", "d1.bin"), null)).Hash;
        var cdex = Scan(root =>
        {
            foreach (var f in root.Children)
            {
                f.SetHash(hash);
                f.IsPartialHash = false;
            }
        });
        var repository = Substitute.For<ICatalogRepository>();
        repository.GetColumnarFileList(Arg.Any<IEnumerable<string>>()).Returns([cdex]);
        var warnings = Substitute.For<Serilog.ILogger>();
        var duplication = new Duplication(Substitute.For<ILogger>(), Config(), Substitute.For<IApplicationDiagnostics>(), new OperationCancellation());
        var handler = new FindDuplicateCommandHandler(duplication, repository, warnings);

        await RunQuietly(handler);
        warnings.DidNotReceive().Warning(Arg.Is<string>(s => s.Contains("older cde version")), Arg.Any<string>());

        LegacyCatalog.MarkPre222(cdex);
        await RunQuietly(handler);
        warnings.Received(1).Warning(Arg.Is<string>(s => s.Contains("older cde version")), Arg.Is<string>(f => f == cdex));
    }

    private static async Task RunQuietly(FindDuplicateCommandHandler handler)
    {
        var original = Console.Out;
        Console.SetOut(TextWriter.Null);
        try { await handler.OnHandle(new FindDuplicatesCommand(), default); }
        finally { Console.SetOut(original); }
    }

    [Test]
    public async Task Printed_hash_is_the_canonical_XxHash128_hex()
    {
        var bytes = Bytes(5000, 9);
        var file = Put("x.bin", bytes);
        var cdex = Scan();
        var response = await new HashHelper(Substitute.For<ILogger>()).GetHashResponseFromFile(file, null);
        var writer = new FieldRecorder();

        using var r = new ColumnarCatalogReader(cdex);
        EntryRecords.WriteHashed(writer, cdex, r, Index(r, "x.bin"), response.Hash, partial: false);

        Assert.That(writer.Fields["hash"], Is.EqualTo(Convert.ToHexStringLower(XxHash128.Hash(bytes))));
    }

    private sealed class FieldRecorder : IRecordWriter
    {
        public Dictionary<string, string> Fields { get; } = [];

        public void BeginRecord() { }
        public void Field(string name, ReadOnlySpan<char> value) => Fields[name] = value.ToString();
        public void Field(string name, long value) => Fields[name] = value.ToString();
        public void Field(string name, bool value) => Fields[name] = value.ToString();
        public void Field(string name, DateTime value) => Fields[name] = value.ToString("O");
        public void EndRecord() { }
        public void Flush() { }
    }

    private static RootEntry Tree(bool withHashes)
    {
        var root = new RootEntry { Path = @"C:\data" };
        var link = new DirEntry(true) { Path = "link", IsReparsePoint = true };
        root.AddChild(link);
        var full = new DirEntry(false) { Path = "full.bin", Size = 100, Modified = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc) };
        var partial = new DirEntry(false) { Path = "partial.bin", Size = 200, Modified = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc) };
        var plain = new DirEntry(false) { Path = "plain.bin", Size = 300, Modified = new DateTime(2020, 1, 2, 3, 4, 7, DateTimeKind.Utc) };
        if (withHashes)
        {
            full.SetHash(new Hash16(1, 2));
            partial.SetHash(new Hash16(3, 4));
            partial.IsPartialHash = true;
        }

        root.AddChild(full);
        root.AddChild(partial);
        link.AddChild(plain);
        root.SetInMemoryFields();
        return root;
    }

    private string WriteTree(RootEntry root)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    private static DirEntry File_(RootEntry root, string name)
    {
        var stack = new Stack<IList<DirEntry>>();
        stack.Push(root.Children);
        while (stack.Count > 0)
        {
            foreach (var d in stack.Pop())
            {
                if (d.Path == name) return d;
                if (d.IsDirectory && d.Children != null) stack.Push(d.Children);
            }
        }

        throw new InvalidOperationException(name);
    }

    private static int Index(ColumnarCatalogReader r, string name) =>
        Enumerable.Range(1, r.Count - 1).First(i => r.NameOf(i) == name);

    private static byte[] Bytes(int size, int seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    private string Put(string name, byte[] content)
    {
        var path = Path.Combine(_dir, "data", name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private byte[] FileBytes(string name) => File.ReadAllBytes(Path.Combine(_dir, "data", name));

    private static Hash16 Expected(ReadOnlySpan<byte> bytes) => new(XxHash128.HashToUInt128(bytes));

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(FirstPass);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        return config;
    }

    private string Scan(Action<RootEntry> before = null)
    {
        var root = new RootEntry(Config());
        root.PopulateRoot(Path.Combine(_dir, "data"));
        before?.Invoke(root);
        var cdex = Path.Combine(_dir, "data.cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    private static async Task HashAndSave(string cdex, ILogger logger, Action<string> status = null)
    {
        var duplication = new Duplication(logger, Config(), Substitute.For<IApplicationDiagnostics>(), new OperationCancellation())
        {
            StatusMessageEvent = status,
        };
        var tmp = cdex + ".tmp";
        using (var reader = new ColumnarCatalogReader(cdex))
        {
            var updates = await duplication.ApplyHash([reader]);
            ColumnarFormat.WriteWithHashes(reader, updates.For(0), cdex, tmp);
        }

        File.Move(tmp, cdex, overwrite: true);
    }
}
