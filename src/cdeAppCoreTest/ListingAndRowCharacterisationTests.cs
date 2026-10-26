using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Search;
using cdeLib;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeAppCoreTest;

/// <summary>
/// Pins directory-listing (<c>GetChildren</c>) and search-row behaviour on a drive-root catalog with
/// non-ASCII names, empty/file-only/folder-bearing directories and a large folder, plus allocation
/// guards: a page of children must allocate for that page only, whatever the folder size.
/// </summary>
[TestFixture]
public class ListingAndRowCharacterisationTests
{
    private static RootEntry BuildTree(string rootPath, int bigCount = 300)
    {
        var root = new RootEntry
        {
            Path = rootPath, VolumeName = "VOL", DefaultFileName = "t.cde", ActualFileName = "t.cde", DriveLetterHint = "C",
        };

        var dirA = new DirEntry(true) { Path = "dirA" };
        var sub = new DirEntry(true) { Path = "sub" };
        sub.AddChild(new DirEntry(false) { Path = "f.txt", Size = 7 });
        dirA.AddChild(sub);
        dirA.AddChild(new DirEntry(false) { Path = "a.txt", Size = 1 });

        var empty = new DirEntry(true) { Path = "emptyDir" };

        var filesOnly = new DirEntry(true) { Path = "filesOnly" };
        filesOnly.AddChild(new DirEntry(false) { Path = "one.bin", Size = 2 });
        filesOnly.AddChild(new DirEntry(false) { Path = "two.bin", Size = 3 });

        var cafe = new DirEntry(true) { Path = "caf\u00e9" };
        cafe.AddChild(new DirEntry(false) { Path = "r\u00e9sum\u00e9 \u65e5\u672c.txt", Size = 9 });
        cafe.AddChild(new DirEntry(false) { Path = "\U0001F600.log", Size = 11 });

        // Large folder: mixed files and sub-folders so sibling order, folders-only and paging are exercised.
        var many = new DirEntry(true) { Path = "many" };
        for (var n = 0; n < bigCount; n++)
        {
            if (n % 7 == 0)
            {
                var d = new DirEntry(true) { Path = $"folder_{n:D6}" };
                if (n % 14 == 0) d.AddChild(new DirEntry(false) { Path = "x.dat", Size = n });
                else d.AddChild(new DirEntry(true) { Path = "inner" });
                many.AddChild(d);
            }
            else
            {
                many.AddChild(new DirEntry(false) { Path = $"file_{n:D6}.dat", Size = n });
            }
        }

        root.AddChild(dirA);
        root.AddChild(empty);
        root.AddChild(filesOnly);
        root.AddChild(cafe);
        root.AddChild(many);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt", Size = 5 });
        root.SetInMemoryFields();
        return root;
    }

    private static DirectoryNodeDto Child(CatalogFixture f, EntryRefDto parent, string name)
        => f.Session.GetChildren(parent).Single(c => c.Name == name);

    [TestCase(@"C:\", @"C:\")]
    [TestCase(@"C:\test", @"C:\test\")]
    [TestCase(@"\\server\share", @"\\server\share\")]
    [TestCase(@"\\server\share\", @"\\server\share\")]
    public void GetChildren_FullPathsAndFields(string root, string prefix)
    {
        using var f = new CatalogFixture(BuildTree(root));
        var top = f.Session.GetChildren(new EntryRefDto(0, 0));
        Assert.That(top.Select(c => c.Name), Is.EquivalentTo(new[]
            { "dirA", "emptyDir", "filesOnly", "caf\u00e9", "many", "root_file.txt" }));

        foreach (var c in top)
            Assert.That(c.FullPath, Is.EqualTo(f.Session.ResolveFullPath(c.Ref)));

        Assert.That(top.Single(c => c.Name == "dirA").FullPath, Is.EqualTo(prefix + "dirA"));
        var dirA = Child(f, new EntryRefDto(0, 0), "dirA");
        Assert.That(dirA.HasChildren, Is.True);   // has the 'sub' folder
        Assert.That(dirA.IsDirectory, Is.True);
        Assert.That(Child(f, new EntryRefDto(0, 0), "emptyDir").HasChildren, Is.False);
        Assert.That(Child(f, new EntryRefDto(0, 0), "filesOnly").HasChildren, Is.False); // files don't count
        Assert.That(Child(f, new EntryRefDto(0, 0), "many").HasChildren, Is.True);
        var file = Child(f, new EntryRefDto(0, 0), "root_file.txt");
        Assert.That(file.HasChildren, Is.False);
        Assert.That(file.IsDirectory, Is.False);
        Assert.That(file.Size, Is.EqualTo(5));
        Assert.That(file.FullPath, Is.EqualTo(prefix + "root_file.txt"));

        var cafe = Child(f, new EntryRefDto(0, 0), "caf\u00e9");
        Assert.That(cafe.FullPath, Is.EqualTo(prefix + "caf\u00e9"));
        var kids = f.Session.GetChildren(cafe.Ref);
        Assert.That(kids.Select(k => k.FullPath), Is.EquivalentTo(new[]
        {
            prefix + "caf\u00e9\\r\u00e9sum\u00e9 \u65e5\u672c.txt",
            prefix + "caf\u00e9\\\U0001F600.log",
        }));
        Assert.That(kids.Select(k => k.Ref.CatalogId), Is.All.EqualTo(0));
    }

    [Test]
    public void GetChildren_RootNode_NotFlaggedDirectory_HasNoHasChildren()
    {
        // GetPath surfaces the root via the same DTO mapping; the root is not flagged a directory.
        using var f = new CatalogFixture(BuildTree(@"C:\"));
        var many = Child(f, new EntryRefDto(0, 0), "many");
        var path = f.Session.GetPath(many.Ref);
        Assert.That(path.Select(p => p.Name), Is.EqualTo(new[] { @"C:\", "many" }));
        Assert.That(path.Select(p => p.FullPath), Is.EqualTo(new[] { @"C:\", @"C:\many" }));
        Assert.That(path[0].IsDirectory, Is.EqualTo(false));
        Assert.That(path[0].HasChildren, Is.False);
        Assert.That(path[1].HasChildren, Is.True);
    }

    [Test]
    public void GetChildren_OfFileAndEmptyDir_IsEmpty()
    {
        using var f = new CatalogFixture(BuildTree(@"C:\"));
        Assert.That(f.Session.GetChildren(Child(f, new EntryRefDto(0, 0), "emptyDir").Ref), Is.Empty);
        Assert.That(f.Session.GetChildren(Child(f, new EntryRefDto(0, 0), "root_file.txt").Ref), Is.Empty);
    }

    [Test]
    public void GetChildren_Paging_MatchesSliceOfFullListing(
        [Values(false, true)] bool foldersOnly,
        [Values(0, 1, 5, 299, 300, 1000)] int skip,
        [Values(0, 1, 7, 100, int.MaxValue)] int take)
    {
        using var f = new CatalogFixture(BuildTree(@"C:\"));
        var many = Child(f, new EntryRefDto(0, 0), "many").Ref;

        var all = f.Session.GetChildren(many, foldersOnly);
        var expected = all.Skip(skip).Take(take).ToList();
        var page = f.Session.GetChildren(many, foldersOnly, skip, take);

        Assert.That(page.Select(p => p.Ref), Is.EqualTo(expected.Select(p => p.Ref)));
        Assert.That(page.Select(p => p.Name), Is.EqualTo(expected.Select(p => p.Name)));
        for (var i = 0; i < page.Count; i++) AssertSame(expected[i], page[i]);
    }

    [Test]
    public void GetChildren_FoldersOnly_AndCounts()
    {
        using var f = new CatalogFixture(BuildTree(@"C:\", 300));
        var many = Child(f, new EntryRefDto(0, 0), "many").Ref;
        var all = f.Session.GetChildren(many);
        var folders = f.Session.GetChildren(many, foldersOnly: true);
        Assert.That(all, Has.Count.EqualTo(300));
        Assert.That(folders, Has.Count.EqualTo(43)); // n % 7 == 0 for n in [0,300)
        Assert.That(folders.Select(x => x.Ref), Is.EqualTo(all.Where(x => x.IsDirectory).Select(x => x.Ref)));
        // folder_000000 holds a file only; folder_000007 holds a folder.
        Assert.That(folders.Single(x => x.Name == "folder_000000").HasChildren, Is.False);
        Assert.That(folders.Single(x => x.Name == "folder_000007").HasChildren, Is.True);
        // negative / zero take behave as Take() did.
        Assert.That(f.Session.GetChildren(many, false, 0, 0), Is.Empty);
    }

    private static void AssertSame(DirectoryNodeDto a, DirectoryNodeDto b)
    {
        Assert.That(b.Ref, Is.EqualTo(a.Ref));
        Assert.That(b.Name, Is.EqualTo(a.Name));
        Assert.That(b.FullPath, Is.EqualTo(a.FullPath));
        Assert.That(b.IsDirectory, Is.EqualTo(a.IsDirectory));
        Assert.That(b.HasChildren, Is.EqualTo(a.HasChildren));
        Assert.That(b.Size, Is.EqualTo(a.Size));
        Assert.That(b.Modified, Is.EqualTo(a.Modified));
        Assert.That(b.IsModifiedBad, Is.EqualTo(a.IsModifiedBad));
        Assert.That(b.IsReparsePoint, Is.EqualTo(a.IsReparsePoint));
    }

    // ---- search rows ----

    [TestCase(@"C:\", @"C:\")]
    [TestCase(@"C:\test", @"C:\test\")]
    [TestCase(@"\\server\share", @"\\server\share\")]
    public async Task SearchRows_FullAndParentPath(string root, string prefix)
    {
        using var f = new CatalogFixture(BuildTree(root));
        var search = new SearchService(f.Session);
        var rows = new List<SearchResultRow>();
        await foreach (var r in search.SearchAsync(new SearchQuery { Pattern = "r\u00e9sum\u00e9" })) rows.Add(r);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Name, Is.EqualTo("r\u00e9sum\u00e9 \u65e5\u672c.txt"));
        Assert.That(rows[0].ParentPath, Is.EqualTo(prefix + "caf\u00e9"));
        Assert.That(rows[0].FullPath, Is.EqualTo(prefix + "caf\u00e9\\r\u00e9sum\u00e9 \u65e5\u672c.txt"));
        Assert.That(rows[0].Size, Is.EqualTo(9));
        Assert.That(rows[0].FullPath, Is.EqualTo(f.Session.ResolveFullPath(rows[0].Ref)));

        rows.Clear();
        await foreach (var r in search.SearchAsync(new SearchQuery { Pattern = "root_file" })) rows.Add(r);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].ParentPath, Is.EqualTo(root));
        Assert.That(rows[0].FullPath, Is.EqualTo(prefix + "root_file.txt"));
    }

    // ---- allocation guards ----

    private static long Best(Action action, int runs = 5)
    {
        action(); // warm up
        var best = long.MaxValue;
        for (var r = 0; r < runs; r++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return best;
    }

    [Test]
    public void GetChildren_Page_AllocatesForThePageOnly_RegardlessOfFolderSize()
    {
        using var small = new CatalogFixture(BuildTree(@"C:\", 1_000));
        using var large = new CatalogFixture(BuildTree(@"C:\", 100_000));
        var smallMany = Child(small, new EntryRefDto(0, 0), "many").Ref;
        var largeMany = Child(large, new EntryRefDto(0, 0), "many").Ref;

        foreach (var foldersOnly in new[] { false, true })
        {
            var a = Best(() => small.Session.GetChildren(smallMany, foldersOnly, 0, 20));
            var b = Best(() => large.Session.GetChildren(largeMany, foldersOnly, 0, 20));
            Assert.That(b - a, Is.LessThan(256), $"foldersOnly={foldersOnly}: small={a} large={b}");
            Assert.That(b, Is.LessThan(20 * 1024), $"foldersOnly={foldersOnly}: page of 20 allocated {b}");
        }
    }

    [Test]
    public void GetChildren_HasChildrenCheck_DoesNotScaleWithChildDirectorySize()
    {
        // 'many' children folders hold only files (long child lists, no sub-folder): has-children must
        // walk them without materialising anything.
        static RootEntry Tree(int filesPerDir)
        {
            var root = new RootEntry { Path = @"C:\", VolumeName = "V", DefaultFileName = "h.cde", ActualFileName = "h.cde", DriveLetterHint = "C" };
            for (var d = 0; d < 10; d++)
            {
                var dir = new DirEntry(true) { Path = $"d{d}" };
                for (var n = 0; n < filesPerDir; n++) dir.AddChild(new DirEntry(false) { Path = $"f{n:D6}.dat" });
                root.AddChild(dir);
            }
            root.SetInMemoryFields();
            return root;
        }

        using var small = new CatalogFixture(Tree(10));
        using var large = new CatalogFixture(Tree(20_000));
        var root0 = new EntryRefDto(0, 0);
        var a = Best(() => small.Session.GetChildren(root0));
        var b = Best(() => large.Session.GetChildren(root0));
        Assert.That(small.Session.GetChildren(root0).Select(c => c.HasChildren), Is.All.False);
        Assert.That(large.Session.GetChildren(root0).Select(c => c.HasChildren), Is.All.False);
        Assert.That(b - a, Is.LessThan(256), $"small={a} large={b}");
    }
}
