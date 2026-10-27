using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Serialization;
using MessagePack;
using NSubstitute;
using NUnit.Framework;
using EFlags = cdeLib.Entities.Flags;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #37 compatibility: a partial hash made before the fix (PartialHash bit, no PartialHashV2 bit) hashed the
/// wrong block. It must read as "not hashed", be recomputed by the next hash run and never compare equal to a
/// new-style partial hash.
/// </summary>
[NonParallelizable]
public class PartialHashCompatibilityTests
{
    private class TestDuplication : Duplication
    {
        public TestDuplication(ILogger logger, IConfiguration configuration, IApplicationDiagnostics diagnostics)
            : base(logger, configuration, diagnostics, new OperationCancellation())
        {
        }

        public DuplicationStatistics Stats => _duplicationStatistics;
    }

    private static DirEntry NewStyle(string name, ulong hash, bool partial = true)
    {
        var de = new DirEntry(false) { Path = name, Size = 10 };
        de.SetHash(hash);
        de.IsPartialHash = partial;
        return de;
    }

    private static DirEntry Legacy(string name, ulong hash)
    {
        // exactly the bits an old catalog holds: HashDone | PartialHash, no PartialHashV2.
        var de = new DirEntry(false) { Path = name, Size = 10, BitFields = EFlags.HashDone | EFlags.PartialHash };
        de.Hash = new Hash16(hash, 0);
        return de;
    }

    [Test]
    public void Setting_partial_sets_the_new_bit_and_clearing_removes_it()
    {
        var de = NewStyle("a", 5);
        Assert.That(de.BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
        Assert.That(de.IsHashDone, Is.True);

        de.IsPartialHash = false; // promoted to a full hash
        Assert.That(de.BitFields & (EFlags.PartialHash | EFlags.PartialHashV2), Is.EqualTo(EFlags.None));
        Assert.That(de.IsHashDone, Is.True);
    }

    [Test]
    public void Legacy_partial_reads_as_not_hashed_but_legacy_full_is_unaffected()
    {
        Assert.That(Legacy("a", 5).IsHashDone, Is.False);

        var full = new DirEntry(false) { Path = "f", BitFields = EFlags.HashDone };
        Assert.That(full.IsHashDone, Is.True);
    }

    [Test]
    public void Legacy_partial_never_equals_a_new_style_partial_or_another_legacy()
    {
        var cmp = new CommonEntryEqualityComparer();
        Assert.That(cmp.Equals(Legacy("a", 5), NewStyle("b", 5)), Is.False);
        Assert.That(cmp.Equals(Legacy("a", 5), Legacy("b", 5)), Is.False);
        Assert.That(cmp.Equals(NewStyle("a", 5), NewStyle("b", 5)), Is.True);
    }

    [Test]
    public void Legacy_partial_is_not_copied_on_rescan_but_new_partial_is_with_its_flag()
    {
        var cfg = Substitute.For<IConfiguration>();
        cfg.ProgressUpdateInterval.Returns(5000);

        RootEntry Tree(DirEntry file)
        {
            var root = new RootEntry(cfg) { Path = @"C:\" };
            file.Modified = new DateTime(2011, 5, 1, 12, 11, 10);
            root.AddChild(file);
            return root;
        }

        var legacySrc = Tree(Legacy("f", 7));
        var dest = Tree(new DirEntry(false) { Path = "f", Size = 10 });
        legacySrc.TraverseTreesCopyHash(dest);
        Assert.That(dest.Children[0].IsHashDone, Is.False, "legacy partial must not be reused");

        var newSrc = Tree(NewStyle("f", 7));
        dest = Tree(new DirEntry(false) { Path = "f", Size = 10 });
        newSrc.TraverseTreesCopyHash(dest);
        Assert.That(dest.Children[0].IsHashDone, Is.True);
        Assert.That(dest.Children[0].IsPartialHash, Is.True);
        Assert.That(dest.Children[0].BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
    }

    [Test]
    public void New_flag_round_trips_through_cdex_and_legacy_partial_stays_legacy()
    {
        var root = new RootEntry { Path = @"C:\test" };
        root.AddChild(NewStyle("new.bin", 11));
        root.AddChild(Legacy("old.bin", 22));
        root.AddChild(NewStyle("full.bin", 33, partial: false));
        root.SetInMemoryFields();

        var path = Path.Combine(Path.GetTempPath(), $"cdetest-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(EntryStore.Build(root), path);
            using var reader = new ColumnarCatalogReader(path);
            int Find(string n) => Enumerable.Range(0, reader.Count).First(i => reader.NameOf(i) == n);

            var n = Find("new.bin");
            Assert.That(reader.Flags(n) & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
            Assert.That(reader.IsHashDone(n), Is.True);
            Assert.That(reader.IsPartialHash(n), Is.True);

            var o = Find("old.bin");
            Assert.That(reader.Flags(o) & EFlags.PartialHashV2, Is.EqualTo(EFlags.None));
            Assert.That(reader.IsHashDone(o), Is.False);

            Assert.That(reader.IsHashDone(Find("full.bin")), Is.True);

            // back into a tree as `cde hash` does: flag survives, legacy still reads as not hashed.
            var tree = CatalogTreeBuilder.FromSource(reader);
            var newBack = tree.Children.First(c => c.Path == "new.bin");
            Assert.That(newBack.IsHashDone && newBack.IsPartialHash, Is.True);
            Assert.That(newBack.BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
            Assert.That(newBack.Hash, Is.EqualTo(new Hash16(11UL, 0)));
            Assert.That(tree.Children.First(c => c.Path == "old.bin").IsHashDone, Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void New_flag_round_trips_through_messagepack_cde()
    {
        var root = new RootEntry { Path = @"C:\test" };
        root.AddChild(NewStyle("new.bin", 11));
        root.AddChild(Legacy("old.bin", 22));

        var bytes = MessagePackSerializer.Serialize(root, MessagePackConfig.Options);
        var back = MessagePackSerializer.Deserialize<RootEntry>(bytes, MessagePackConfig.Options);

        var n = back.Children.First(c => c.Path == "new.bin");
        Assert.That(n.IsHashDone && n.IsPartialHash, Is.True);
        Assert.That(n.BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
        Assert.That(back.Children.First(c => c.Path == "old.bin").IsHashDone, Is.False);
    }

    [Test]
    public async Task Hash_pipeline_recomputes_legacy_partials_and_marks_them_new_style()
    {
        var dir = nameof(PartialHashCompatibilityTests) + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(dir);
        try
        {
            // same size, differ in the FIRST block so partial hashes differ and the entries stay partial.
            var a = new byte[4096];
            new Random(1).NextBytes(a);
            var b = (byte[])a.Clone();
            b[0] ^= 0xFF;
            await File.WriteAllBytesAsync(Path.Combine(dir, "a.bin"), a);
            await File.WriteAllBytesAsync(Path.Combine(dir, "b.bin"), b);

            var config = Substitute.For<IConfiguration>();
            config.ProgressUpdateInterval.Returns(100);
            config.HashFirstPassSize.Returns(1024);
            config.DegreesOfParallelism.Returns(1);
            config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
            var logger = Substitute.For<ILogger>();

            var root = new RootEntry(config);
            root.PopulateRoot(dir);

            TestDuplication NewDup() => new(logger, config, Substitute.For<IApplicationDiagnostics>());

            await NewDup().ApplyHash([root]);
            var files = root.Children.Where(c => !c.IsDirectory).ToList();
            Assert.That(files, Has.Count.EqualTo(2));
            Assert.That(files.All(f => f.IsHashDone && f.IsPartialHash && (f.BitFields & EFlags.PartialHashV2) != 0), Is.True);
            var expected = files.ToDictionary(f => f.Path, f => f.Hash);

            // turn them into old-catalog partials: bit absent, hash value from the wrong block.
            foreach (var f in files)
            {
                f.BitFields &= ~EFlags.PartialHashV2;
                f.Hash = new Hash16(0xBADBADUL, 0);
            }
            Assert.That(files.Any(f => f.IsHashDone), Is.False);

            var second = NewDup();
            await second.ApplyHash([root]);

            Assert.That(second.Stats.PartialHashes, Is.EqualTo(2), "legacy partials must be recomputed");
            foreach (var f in files)
            {
                Assert.That(f.IsHashDone && f.IsPartialHash, Is.True);
                Assert.That(f.BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
                Assert.That(f.Hash, Is.EqualTo(expected[f.Path]));
            }

            // a third run finds them current and does no partial work.
            var third = NewDup();
            await third.ApplyHash([root]);
            Assert.That(third.Stats.PartialHashes, Is.EqualTo(0));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void Columnar_hash_reuse_skips_legacy_partial_but_copies_new_partial_and_full()
    {
        var modified = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        RootEntry Tree(Func<string, DirEntry> make)
        {
            var root = new RootEntry { Path = @"C:	est" };
            foreach (var n in new[] { "legacy.bin", "newpartial.bin", "full.bin" })
            {
                var de = make(n);
                de.Path = n;
                de.Size = 100;
                de.Modified = modified;
                root.AddChild(de);
            }
            root.SetInMemoryFields();
            return root;
        }

        var old = Tree(n => n switch
        {
            "legacy.bin" => Legacy(n, 1),
            "newpartial.bin" => NewStyle(n, 2),
            _ => NewStyle(n, 3, partial: false),
        });

        var path = Path.Combine(Path.GetTempPath(), $"cdetest-{Guid.NewGuid():N}.cdex");
        try
        {
            ColumnarFormat.Write(EntryStore.Build(old), path);
            var fresh = Tree(_ => new DirEntry(false));
            using (var reader = new ColumnarCatalogReader(path))
                ColumnarHashReuse.CopyHashes(reader, fresh);

            DirEntry Get(string n) => (DirEntry)fresh.Children.First(c => c.Path == n);
            Assert.That(Get("legacy.bin").IsHashDone, Is.False, "legacy partial must not be reused");
            var np = Get("newpartial.bin");
            Assert.That(np.IsHashDone && np.IsPartialHash, Is.True);
            Assert.That(np.BitFields & EFlags.PartialHashV2, Is.EqualTo(EFlags.PartialHashV2));
            Assert.That(np.Hash, Is.EqualTo(new Hash16(2UL, 0)));
            var full = Get("full.bin");
            Assert.That(full.IsHashDone && !full.IsPartialHash, Is.True);
            Assert.That(full.Hash, Is.EqualTo(new Hash16(3UL, 0)));
        }
        finally { File.Delete(path); }
    }
}
