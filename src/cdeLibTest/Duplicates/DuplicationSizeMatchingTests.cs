using System;
using System.Collections.Generic;
using System.Linq;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;

namespace cdeLibTest.Duplicates;

[TestFixture]
[NonParallelizable]
public class DuplicationSizeMatchingTests
{
    private static Duplication NewDuplication()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(5000);
        return new Duplication(Substitute.For<ILogger>(), config, Substitute.For<IApplicationDiagnostics>(),
            new OperationCancellation());
    }

    private static DirEntry File_(string name, long size) => new(false) { Path = name, Size = size };

    [Test]
    public void GetSizePairs_CreatesPairsOnlyForRepeatedSizes()
    {
        var root = new RootEntry { Path = @"C:\" };
        // sizes: 10 x3, 20 x2, and 500 unique sizes (each seen once), plus zero-size and a directory.
        foreach (var s in new long[] { 10, 20, 10, 20, 10 })
            root.AddChild(File_($"rep{root.Children?.Count ?? 0}", s));
        for (var i = 0; i < 500; i++)
            root.AddChild(File_($"uniq{i}", 1000 + i));
        root.AddChild(File_("empty1", 0));
        root.AddChild(File_("empty2", 0));
        root.AddChild(new DirEntry(true) { Path = "dir" });
        root.SetInMemoryFields();

        var d = NewDuplication();
        var pairs = d.GetSizePairs([root]);

        Assert.That(pairs.Keys.OrderBy(k => k), Is.EqualTo(new long[] { 10, 20 }));
        Assert.That(pairs[10], Has.Count.EqualTo(3));
        Assert.That(pairs[20], Has.Count.EqualTo(2));
        // No pair objects were created for the 500 unique-size files, zero-size files or directories.
        Assert.That(d.PairDirEntriesCreated, Is.EqualTo(5));
    }

    [Test]
    public void GetSizePairs_PreservesFirstSeenSizeOrderAndEntryOrder()
    {
        var root = new RootEntry { Path = @"C:\" };
        var sub = new DirEntry(true) { Path = "sub" };
        root.AddChild(File_("a", 30));
        root.AddChild(File_("b", 5));
        root.AddChild(sub);
        root.AddChild(File_("c", 30));
        sub.AddChild(File_("d", 5));
        sub.AddChild(File_("e", 30));
        root.SetInMemoryFields();

        var pairs = NewDuplication().GetSizePairs([root]);

        Assert.That(pairs.Keys, Is.EqualTo(new long[] { 30, 5 }));
        Assert.That(pairs[30].Select(p => p.ChildDE.Path), Is.EqualTo(new[] { "a", "c", "e" }));
        Assert.That(pairs[5].Select(p => p.ChildDE.Path), Is.EqualTo(new[] { "b", "d" }));
    }

    [Test]
    public void GroupByDriveRoot_GroupsByDriveOrShareInFirstSeenOrder_WithoutBuildingPathStrings()
    {
        var c1 = new RootEntry { Path = @"C:\data" };
        var c2 = new RootEntry { Path = @"C:\other" };
        var d1 = new RootEntry { Path = @"D:\" };
        var unc = new RootEntry { Path = @"\\srv\share\stuff" };
        var deep = string.Join(@"\", Enumerable.Repeat("a-rather-long-directory-name", 6));
        var list = new List<PairDirEntry>();
        const int perRoot = 5000;
        foreach (var root in new[] { c1, d1, c2, unc })
        {
            var dir = new DirEntry(true) { Path = deep };
            root.AddChild(dir);
            for (var i = 0; i < perRoot; i++)
            {
                var f = File_($"file{i}.bin", 100);
                dir.AddChild(f);
                list.Add(new PairDirEntry(dir, f));
            }

            root.SetInMemoryFields();
        }

        // interleave so group membership must come from the entry, not list position
        var shuffled = list.OrderBy(p => p.ChildDE.Path.GetHashCode()).ToList();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var groups = Duplication.GroupByDriveRoot(shuffled);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(groups, Has.Count.EqualTo(3)); // C:\ (c1+c2), D:\, \\srv\share
        Assert.That(groups.Sum(g => g.Count), Is.EqualTo(list.Count));
        var cGroup = groups.Single(g => g.Any(p => p.GetRootEntry() == c1));
        Assert.That(cGroup.Any(p => p.GetRootEntry() == c2), Is.True);
        Assert.That(cGroup, Has.Count.EqualTo(perRoot * 2));
        // first-seen order of groups follows the input list
        Assert.That(groups[0][0], Is.SameAs(shuffled[0]));
        // Building one ~190 char path per pair would allocate > 7 MB for 20k pairs; grouping must not.
        Assert.That(allocated, Is.LessThan(1024 * 1024));
    }
}
