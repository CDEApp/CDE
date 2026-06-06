using System.Collections.Generic;
using System.Linq;
using cdeLib.Entities;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Soa;

/// <summary>
/// Proves the <see cref="EntryRef"/> adapter lets the existing tree-oriented, ICommonEntry-based
/// code run unchanged on the struct-of-arrays <see cref="EntryStore"/> — same traversal results,
/// same paths, same counts as the pointer tree.
/// </summary>
[TestFixture]
public class EntryRefTests
{
    private static RootEntry BuildTree()
    {
        var root = new RootEntry { Path = @"C:\test" };

        var dir1 = new DirEntry(true) { Path = "dir1" };
        dir1.AddChild(new DirEntry(false) { Path = "alpha.txt", Size = 10 });
        dir1.AddChild(new DirEntry(false) { Path = "beta.log", Size = 20 });

        var docs = new DirEntry(true) { Path = "docs" };
        docs.AddChild(new DirEntry(false) { Path = "alpha.md", Size = 30 });

        root.AddChild(dir1);
        root.AddChild(docs);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt", Size = 40 });

        root.SetInMemoryFields();
        return root;
    }

    private static List<string> TraverseFullPaths(ICommonEntry root)
    {
        var paths = new List<string>();
        EntryHelper.TraverseTreePair(root, (_, child) => { paths.Add(child.FullPath); return true; });
        paths.Sort();
        return paths;
    }

    [Test]
    public void TraverseTreePair_OverEntryRef_MatchesTreeTraversal()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        var storeRoot = new EntryRef(store, 0);

        Assert.That(TraverseFullPaths(storeRoot), Is.EqualTo(TraverseFullPaths(root)));
    }

    [Test]
    public void Children_OverEntryRef_MatchTreeChildren()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        var storeRoot = new EntryRef(store, 0);

        var treeChildNames = root.Children.Select(c => c.Path).OrderBy(x => x).ToList();
        var storeChildNames = storeRoot.Children.Select(c => c.Path).OrderBy(x => x).ToList();
        Assert.That(storeChildNames, Is.EqualTo(treeChildNames));

        // A file has no children view.
        var rootFile = storeRoot.Children.First(c => c.Path == "root_file.txt");
        Assert.That(rootFile.IsDirectory, Is.False);
        Assert.That(rootFile.Children, Is.Null);
    }

    [Test]
    public void Counts_OverEntryRef_MatchTree()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        var storeRoot = new EntryRef(store, 0);

        Assert.That(storeRoot.FileEntryCount, Is.EqualTo(root.FileEntryCount));
        Assert.That(storeRoot.DirEntryCount, Is.EqualTo(root.DirEntryCount));
        Assert.That(storeRoot.FileEntryCount, Is.EqualTo(4u));
        Assert.That(storeRoot.DirEntryCount, Is.EqualTo(2u));
    }

    [Test]
    public void GetListFromRoot_OverEntryRef_GoesRootToLeaf()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        var storeRoot = new EntryRef(store, 0);

        // Find alpha.txt under dir1 and walk back to root.
        var alpha = TraverseFind(store, "alpha.txt");
        var chain = alpha.GetListFromRoot().Select(e => e.Path).ToList();
        Assert.That(chain, Is.EqualTo(new[] { @"C:\test", "dir1", "alpha.txt" }));
        Assert.That(alpha.FullPath, Is.EqualTo(@"C:\test\dir1\alpha.txt"));
    }

    [Test]
    public void MutatingMembers_Throw()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        var e = new EntryRef(store, 1);

        Assert.Throws<System.NotSupportedException>(() => e.Path = "x");
        Assert.Throws<System.NotSupportedException>(() => e.AddChild(new DirEntry(false)));
        Assert.Throws<System.NotSupportedException>(() => e.SetSummaryFields());
    }

    private static EntryRef TraverseFind(EntryStore store, string name)
    {
        for (var i = 1; i < store.Count; i++)
        {
            if (store.Name[i] == name) return new EntryRef(store, i);
        }
        return null;
    }
}
