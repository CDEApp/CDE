using System.Collections.Generic;
using System.Linq;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Soa;

/// <summary>
/// Proves the SoA <see cref="EntryStore"/> + <see cref="EntryStoreSearch"/> produce results identical
/// to the production pointer-tree find. This is the safety gate for moving search onto the store.
/// </summary>
[TestFixture]
public class EntryStoreTests
{
    // Builds a small known catalog:
    //   C:\test
    //   ├─ dir1\        (dir)
    //   │   ├─ alpha.txt
    //   │   └─ beta.log
    //   ├─ docs\        (dir)
    //   │   └─ alpha.md
    //   └─ root_file.txt
    private static RootEntry BuildTree()
    {
        var root = new RootEntry { Path = @"C:\test" };

        var dir1 = new DirEntry(true) { Path = "dir1" };
        dir1.AddChild(new DirEntry(false) { Path = "alpha.txt" });
        dir1.AddChild(new DirEntry(false) { Path = "beta.log" });

        var docs = new DirEntry(true) { Path = "docs" };
        docs.AddChild(new DirEntry(false) { Path = "alpha.md" });

        root.AddChild(dir1);
        root.AddChild(docs);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt" });

        root.SetInMemoryFields();
        return root;
    }

    private static List<string> TreeFind(RootEntry root, string pattern, bool regex, bool path,
        bool files, bool folders)
    {
        var found = new List<string>();
        var options = new FindOptions
        {
            Pattern = pattern,
            RegexMode = regex,
            IncludePath = path,
            IncludeFiles = files,
            IncludeFolders = folders,
            LimitResultCount = int.MaxValue,
            VisitorFunc = (p, d) => { found.Add(p.MakeFullPath(d)); return true; },
        };
        options.Find(new[] { root });
        return found;
    }

    private static List<string> StoreFind(EntryStore store, string pattern, bool regex, bool path,
        bool files, bool folders)
    {
        var found = new List<string>();
        EntryStoreSearch.Find(store, pattern, regex, path, files, folders, i => found.Add(store.FullPath(i)));
        return found;
    }

    [Test]
    public void Build_CountsEveryEntryIncludingRoot()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);
        // 3 dirs/files under root + 2 files under dir1 + 1 under docs = 6, plus the root = 7.
        Assert.That(store.Count, Is.EqualTo(7));
    }

    [Test]
    public void Build_CapturesCatalogMetadata()
    {
        var root = new RootEntry
        {
            Path = @"D:\",
            VolumeName = "DATA",
            DefaultFileName = "D-DATA.cde",
            ActualFileName = @"C:\cat\D-DATA.cde",
            DriveLetterHint = "D",
            Description = "data drive",
            AvailSpace = 111,
            TotalSpace = 222,
        };
        root.AddChild(new DirEntry(false) { Path = "f.txt", Size = 7 });
        root.SetInMemoryFields();

        var store = EntryStore.Build(root);

        Assert.Multiple(() =>
        {
            Assert.That(store.RootPath, Is.EqualTo(@"D:\"));
            Assert.That(store.VolumeName, Is.EqualTo("DATA"));
            Assert.That(store.DefaultFileName, Is.EqualTo("D-DATA.cde"));
            Assert.That(store.ActualFileName, Is.EqualTo(@"C:\cat\D-DATA.cde"));
            Assert.That(store.DriveLetterHint, Is.EqualTo("D"));
            Assert.That(store.Description, Is.EqualTo("data drive"));
            Assert.That(store.AvailSpace, Is.EqualTo(111));
            Assert.That(store.TotalSpace, Is.EqualTo(222));
            Assert.That(store.RootFileEntryCount, Is.EqualTo(root.FileEntryCount));
            Assert.That(store.RootDirEntryCount, Is.EqualTo(root.DirEntryCount));
            Assert.That(store.RootSize, Is.EqualTo(root.Size));
        });
    }

    [Test]
    public void FullPath_MatchesTreeForEveryEntry()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);

        // Every non-root entry's store full path must appear in the tree's full path set.
        var treePaths = TreeFind(root, "", false, false, true, true).OrderBy(x => x).ToList();
        var storePaths = new List<string>();
        for (var i = 1; i < store.Count; i++) storePaths.Add(store.FullPath(i));
        storePaths.Sort();

        Assert.That(storePaths, Is.EqualTo(treePaths));
    }

    [TestCase("alpha", false, false)] // name substring
    [TestCase("txt", false, false)]
    [TestCase("xyzzy", false, false)] // no matches
    [TestCase("", false, false)]      // match all
    [TestCase(@"dir1\alpha", false, true)] // path substring
    [TestCase("docs", false, true)]
    [TestCase(@"\.md$", true, false)]   // regex on name
    [TestCase(@"test\\d", true, true)]  // regex on path
    public void Search_MatchesTreeFind(string pattern, bool regex, bool path)
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);

        var tree = TreeFind(root, pattern, regex, path, true, true).OrderBy(x => x).ToList();
        var soa = StoreFind(store, pattern, regex, path, true, true).OrderBy(x => x).ToList();

        Assert.That(soa, Is.EqualTo(tree), $"pattern='{pattern}' regex={regex} path={path}");
    }

    [Test]
    public void Search_FilesOnly_MatchesTreeFind()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);

        var tree = TreeFind(root, "", false, false, files: true, folders: false).OrderBy(x => x).ToList();
        var soa = StoreFind(store, "", false, false, files: true, folders: false).OrderBy(x => x).ToList();

        Assert.That(soa, Is.EqualTo(tree));
        Assert.That(soa, Has.Count.EqualTo(4)); // alpha.txt, beta.log, alpha.md, root_file.txt
    }

    [Test]
    public void Search_FoldersOnly_MatchesTreeFind()
    {
        var root = BuildTree();
        var store = EntryStore.Build(root);

        var tree = TreeFind(root, "", false, false, files: false, folders: true).OrderBy(x => x).ToList();
        var soa = StoreFind(store, "", false, false, files: false, folders: true).OrderBy(x => x).ToList();

        Assert.That(soa, Is.EqualTo(tree));
        Assert.That(soa, Has.Count.EqualTo(2)); // dir1, docs
    }
}
