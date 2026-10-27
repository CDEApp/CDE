using System;
using System.IO;
using System.Linq;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// Edge cases of <see cref="ColumnarHashReuse"/>: argument checks, empty and oversized directories,
/// duplicate names, and names too long for the stack scratch buffer.
/// </summary>
[TestFixture]
public class ColumnarHashReuseEdgeTests
{
    private static readonly DateTime Stamp = new(2022, 3, 4, 5, 6, 7);

    private static RootEntry NewRoot() => new() { Path = @"C:\test", VolumeName = "V", DefaultFileName = "x.cde" };

    private static DirEntry File(string name, long size, int? hash)
    {
        var e = new DirEntry(false) { Path = name, Size = size, Modified = Stamp };
        if (hash.HasValue)
        {
            e.SetHash(hash.Value);
        }

        return e;
    }

    private static string WriteCdex(RootEntry root)
    {
        root.SetInMemoryFields();
        var path = Path.Combine(Path.GetTempPath(), $"cdehr-edge-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return path;
    }

    private static void CopyThenCleanUp(RootEntry old, RootEntry fresh)
    {
        var path = WriteCdex(old);
        try
        {
            fresh.SetInMemoryFields();
            using var reader = new ColumnarCatalogReader(path);
            ColumnarHashReuse.CopyHashes(reader, fresh);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Test]
    public void CopyHashes_NullSourceOrDestination_Throws()
    {
        var root = NewRoot();
        var path = WriteCdex(root);
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            Assert.Throws<ArgumentException>(() => ColumnarHashReuse.CopyHashes(null, root));
            Assert.Throws<ArgumentException>(() => ColumnarHashReuse.CopyHashes(reader, null));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Test]
    public void CopyHashes_EmptyDestinationDirectory_IsSkipped()
    {
        var old = NewRoot();
        var oldDir = new DirEntry(true) { Path = "dir" };
        oldDir.AddChild(File("a.txt", 10, 111));
        old.AddChild(oldDir);

        var fresh = NewRoot();
        fresh.AddChild(new DirEntry(true) { Path = "dir" });

        Assert.DoesNotThrow(() => CopyThenCleanUp(old, fresh));
        Assert.That(fresh.Children[0].Children, Is.Null.Or.Empty);
    }

    [Test]
    public void CopyHashes_NameMatchesIgnoringCase_AndMissingNamesAreSkipped()
    {
        var old = NewRoot();
        old.AddChild(File("Report.TXT", 10, 111));
        old.AddChild(File("gone.txt", 10, 222));

        var fresh = NewRoot();
        fresh.AddChild(File("report.txt", 10, null));
        fresh.AddChild(File("new.txt", 10, null));

        CopyThenCleanUp(old, fresh);

        var report = fresh.Children.Single(c => c.Path == "report.txt");
        var added = fresh.Children.Single(c => c.Path == "new.txt");
        Assert.That(report.IsHashDone, Is.True);
        Assert.That(added.IsHashDone, Is.False);
    }

    [Test]
    public void CopyHashes_DuplicateNamesInDestination_FirstOneWins()
    {
        var old = NewRoot();
        old.AddChild(File("dup.txt", 10, 111));

        var fresh = NewRoot();
        var first = File("dup.txt", 10, null);
        var second = File("DUP.txt", 10, null);
        fresh.AddChild(first);
        fresh.AddChild(second);

        CopyThenCleanUp(old, fresh);

        Assert.That(first.IsHashDone ^ second.IsHashDone, Is.True, "exactly one duplicate receives the hash");
    }

    [Test]
    public void CopyHashes_NameLongerThanScratchBuffer_StillMatches()
    {
        var longName = new string('n', 300) + ".txt";
        var old = NewRoot();
        old.AddChild(File(longName, 10, 111));
        old.AddChild(File(longName + "2", 10, 222));

        var fresh = NewRoot();
        var match = File(longName, 10, null);
        var other = File(longName + "2", 10, null);
        fresh.AddChild(match);
        fresh.AddChild(other);

        CopyThenCleanUp(old, fresh);

        Assert.That(match.IsHashDone, Is.True);
        Assert.That(other.IsHashDone, Is.True);
    }

    [Test]
    public void CopyHashes_SmallDirectoryAfterHugeOne_StillCopiesHashes()
    {
        const int huge = 4200;
        var old = NewRoot();
        var fresh = NewRoot();
        foreach (var root in new[] { old, fresh })
        {
            var small = new DirEntry(true) { Path = "a_small" };
            var big = new DirEntry(true) { Path = "z_big" };
            var hashed = ReferenceEquals(root, old);
            for (var i = 0; i < 3; i++)
            {
                small.AddChild(File($"s{i}.txt", 10, hashed ? 100 + i : null));
            }

            for (var i = 0; i < huge; i++)
            {
                big.AddChild(File($"b{i}.txt", 10, hashed ? 1000 + i : null));
            }

            root.AddChild(small);
            root.AddChild(big);
        }

        CopyThenCleanUp(old, fresh);

        var freshSmall = fresh.Children.Single(c => c.Path == "a_small");
        var freshBig = fresh.Children.Single(c => c.Path == "z_big");
        Assert.That(freshSmall.Children.All(c => c.IsHashDone), Is.True);
        Assert.That(freshBig.Children.All(c => c.IsHashDone), Is.True);
    }
}
