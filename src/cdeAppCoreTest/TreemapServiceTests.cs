using System.Linq;
using cdeAppCore.Dtos;
using cdeAppCore.Treemap;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeAppCoreTest;

/// <summary>
/// Treemap rows over the fixture catalog (sizes: dir1 = alpha.txt 100 + beta.log 5000 = 5100,
/// docs = alpha.md 200, root_file.txt 50; root = 5350).
/// </summary>
[TestFixture]
public class TreemapServiceTests
{
    private static readonly EntryRefDto Root = new(0, 0);

    private static TreemapRowDto Row(TreemapResponse r, string name) => r.Rows.Single(x => x.Name == name);

    // Every leaf's value sums to the root size: no area is lost or double-counted.
    private static long LeafValueTotal(TreemapResponse r)
    {
        var parents = r.Rows.Select(x => x.ParentId).Where(p => p != null).ToHashSet();
        return r.Rows.Where(x => !parents.Contains(x.NodeId)).Sum(x => x.Value);
    }

    [Test]
    public void Depth1_ListsRootChildrenAsLeaves()
    {
        using var f = new CatalogFixture();
        var r = new TreemapService(f.Session).Build(Root, depth: 1, minFraction: 0);

        Assert.That(r.RootSize, Is.EqualTo(5350));
        Assert.That(r.RootFullPath, Is.EqualTo(@"C:\test"));
        Assert.That(r.Rows.Select(x => x.Name), Is.EquivalentTo(new[] { @"C:\test", "dir1", "docs", "root_file.txt" }));

        var root = r.Rows.Single(x => x.ParentId == null);
        Assert.That(root.Value, Is.Zero, "expanded root contributes no area of its own");
        Assert.That(Row(r, "dir1").Value, Is.EqualTo(5100), "unexpanded dir is a leaf sized to its subtree");
        Assert.That(Row(r, "dir1").IsDirectory, Is.True);
        Assert.That(Row(r, "dir1").ParentId, Is.EqualTo(root.NodeId));
        Assert.That(LeafValueTotal(r), Is.EqualTo(5350));
        Assert.That(r.Truncated, Is.False);
    }

    [Test]
    public void Depth2_ExpandsDirectoriesIntoChildren()
    {
        using var f = new CatalogFixture();
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0);

        Assert.That(Row(r, "dir1").Value, Is.Zero);
        Assert.That(Row(r, "beta.log").Value, Is.EqualTo(5000));
        Assert.That(Row(r, "beta.log").ParentId, Is.EqualTo(Row(r, "dir1").NodeId));
        Assert.That(Row(r, "alpha.md").ParentId, Is.EqualTo(Row(r, "docs").NodeId));
        Assert.That(LeafValueTotal(r), Is.EqualTo(5350));
    }

    [Test]
    public void Rows_CarryEntryRefsUsableForNavigation()
    {
        using var f = new CatalogFixture();
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0);

        var beta = Row(r, "beta.log");
        Assert.That(beta.Ref, Is.Not.Null);
        Assert.That(f.Session.ResolveFullPath(beta.Ref!.Value), Is.EqualTo(@"C:\test\dir1\beta.log"));
        Assert.That(beta.NodeId, Is.EqualTo($"{beta.Ref.Value.CatalogId}-{beta.Ref.Value.EntryIndex}"));
    }

    [Test]
    public void SmallEntries_AreMergedPerFolder()
    {
        using var f = new CatalogFixture();
        // minBytes = 5350 * 0.06 ≈ 321: docs (200) and root_file.txt (50) fold into one row.
        var r = new TreemapService(f.Session).Build(Root, depth: 1, minFraction: 0.06);

        var merged = r.Rows.Single(x => x.MergedCount > 0);
        Assert.That(merged.MergedCount, Is.EqualTo(2));
        Assert.That(merged.Size, Is.EqualTo(250));
        Assert.That(merged.Value, Is.EqualTo(250));
        Assert.That(merged.Name, Is.EqualTo("(2 smaller items)"));
        Assert.That(merged.Ref, Is.Null);
        Assert.That(merged.ParentId, Is.EqualTo(r.Rows.Single(x => x.ParentId == null).NodeId));
        Assert.That(r.Rows.Any(x => x.Name is "docs" or "root_file.txt"), Is.False);
        Assert.That(LeafValueTotal(r), Is.EqualTo(5350));
    }

    [Test]
    public void LoneSmallEntry_IsKeptAsItself()
    {
        using var f = new CatalogFixture();
        // minBytes = ⌈5350 × 0.02⌉ = 107. In dir1, alpha.txt (100) is the only small entry; folding a
        // single entry into "(1 smaller …)" would only hide its name, so it stays as itself.
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0.02);

        Assert.That(r.Rows.Any(x => x.Name == "alpha.txt"), Is.True);
        Assert.That(r.Rows.Any(x => x.MergedCount > 0), Is.False);
    }

    [Test]
    public void MergedRow_OfFilesOnly_IsNamedFiles()
    {
        var tree = new RootEntry { Path = @"C:\m", DefaultFileName = "m.cde", ActualFileName = "m.cde" };
        tree.AddChild(new DirEntry(false) { Path = "big.bin", Size = 1000 });
        tree.AddChild(new DirEntry(false) { Path = "a.txt", Size = 3 });
        tree.AddChild(new DirEntry(false) { Path = "b.txt", Size = 4 });
        tree.SetInMemoryFields();

        using var f = new CatalogFixture(tree);
        var r = new TreemapService(f.Session).Build(Root, depth: 1, minFraction: 0.01); // minBytes 11

        var merged = r.Rows.Single(x => x.MergedCount > 0);
        Assert.That(merged.Name, Is.EqualTo("(2 smaller files)"));
        Assert.That(merged.Size, Is.EqualTo(7));
        Assert.That(merged.IsDirectory, Is.False);
    }

    [Test]
    public void RowCap_MergesTheSmallestAndFlagsTruncation()
    {
        using var f = new CatalogFixture();
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0, maxRows: 3);

        Assert.That(r.Rows, Has.Count.LessThanOrEqualTo(3));
        Assert.That(r.Truncated, Is.True);
        Assert.That(Row(r, "dir1").Value, Is.EqualTo(5100), "dir left unexpanded by the cap becomes a sized leaf");
        Assert.That(r.Rows.Single(x => x.MergedCount > 0).Size, Is.EqualTo(250));
        Assert.That(LeafValueTotal(r), Is.EqualTo(5350));
    }

    [Test]
    public void ZeroSizeEntries_AreOmitted()
    {
        var tree = new RootEntry { Path = @"C:\z", DefaultFileName = "z.cde", ActualFileName = "z.cde" };
        var d = new DirEntry(true) { Path = "d" };
        d.AddChild(new DirEntry(false) { Path = "data.bin", Size = 10 });
        tree.AddChild(d);
        tree.AddChild(new DirEntry(false) { Path = "empty.txt", Size = 0 });
        tree.AddChild(new DirEntry(true) { Path = "emptydir" });
        tree.SetInMemoryFields();

        using var f = new CatalogFixture(tree);
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0);

        Assert.That(r.Rows.Select(x => x.Name), Is.EquivalentTo(new[] { @"C:\z", "d", "data.bin" }));
    }

    [Test]
    public void DriveRoot_KeepsASingleSeparator()
    {
        var tree = new RootEntry { Path = @"C:\", DefaultFileName = "c.cde", ActualFileName = "c.cde" };
        var d = new DirEntry(true) { Path = "Users" };
        d.AddChild(new DirEntry(false) { Path = "a.txt", Size = 10 });
        tree.AddChild(d);
        tree.SetInMemoryFields();

        using var f = new CatalogFixture(tree);
        var r = new TreemapService(f.Session).Build(Root, depth: 2, minFraction: 0);

        Assert.That(r.RootFullPath, Is.EqualTo(@"C:\"));
        Assert.That(r.Rows.Single(x => x.ParentId == null).Name, Is.EqualTo(@"C:\"));
        Assert.That(f.Session.ResolveFullPath(Row(r, "a.txt").Ref!.Value), Is.EqualTo(@"C:\Users\a.txt"));
    }

    [Test]
    public void SubfolderRoot_MapsOnlyThatSubtree()
    {
        using var f = new CatalogFixture();
        var dir1 = f.Session.GetChildren(Root, foldersOnly: true).Single(x => x.Name == "dir1").Ref;
        var r = new TreemapService(f.Session).Build(dir1, depth: 3, minFraction: 0);

        Assert.That(r.RootSize, Is.EqualTo(5100));
        Assert.That(r.RootFullPath, Is.EqualTo(@"C:\test\dir1"));
        Assert.That(r.Rows.Select(x => x.Name), Is.EquivalentTo(new[] { "dir1", "alpha.txt", "beta.log" }));
    }

    [TestCase(0, 1)]
    [TestCase(9, 5)]
    public void Depth_IsClampedToOneThroughFive(int requested, int expected)
    {
        using var f = new CatalogFixture();
        Assert.That(new TreemapService(f.Session).Build(Root, requested, 0).Depth, Is.EqualTo(expected));
    }
}
