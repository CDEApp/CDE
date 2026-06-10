using System.Collections.Generic;
using System.IO;
using System.Linq;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NUnit.Framework;

namespace cdeLibTest.Columnar;

/// <summary>
/// Proves the columnar/mmap format round-trips an <see cref="EntryStore"/> and that its zero-copy
/// byte-scan search returns the same matches as the in-memory store search. This is the safety gate
/// for loading catalogs off the memory map instead of the managed heap.
/// </summary>
[TestFixture]
public class ColumnarCatalogTests
{
    //   C:\test
    //   ├─ dir1\        (dir)
    //   │   ├─ alpha.txt
    //   │   └─ beta.log
    //   ├─ docs\        (dir)
    //   │   └─ alpha.md
    //   └─ root_file.txt
    private static RootEntry BuildTree()
    {
        var root = new RootEntry
        {
            Path = @"C:\test",
            VolumeName = "VOL",
            DefaultFileName = "test.cde",
            DriveLetterHint = "C",
            Description = "desc",
            AvailSpace = 123,
            TotalSpace = 456,
        };

        var dir1 = new DirEntry(true) { Path = "dir1" };
        dir1.AddChild(new DirEntry(false) { Path = "alpha.txt", Size = 100 });
        dir1.AddChild(new DirEntry(false) { Path = "beta.log", Size = 5000 });

        var docs = new DirEntry(true) { Path = "docs" };
        docs.AddChild(new DirEntry(false) { Path = "alpha.md", Size = 200 });

        root.AddChild(dir1);
        root.AddChild(docs);
        root.AddChild(new DirEntry(false) { Path = "root_file.txt", Size = 50 });

        root.SetInMemoryFields();
        return root;
    }

    private static string WriteTemp(EntryStore store)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cdetest-{System.Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, path);
        return path;
    }

    [Test]
    public void RoundTrip_PreservesCountAndMetadata()
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            Assert.That(reader.Count, Is.EqualTo(store.Count));
            Assert.That(reader.RootPath, Is.EqualTo(@"C:\test"));
            Assert.That(reader.VolumeName, Is.EqualTo("VOL"));
            Assert.That(reader.DefaultFileName, Is.EqualTo("test.cde"));
            Assert.That(reader.Description, Is.EqualTo("desc"));
            Assert.That(reader.AvailSpace, Is.EqualTo(123));
            Assert.That(reader.TotalSpace, Is.EqualTo(456));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void FullPath_MatchesStore()
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            for (var i = 0; i < store.Count; i++)
                Assert.That(reader.FullPath(i), Is.EqualTo(store.FullPath(i)), $"path mismatch at {i}");
        }
        finally { File.Delete(path); }
    }

    [TestCase("alpha", true, true)]   // matches files in two dirs
    [TestCase(".txt", true, true)]    // extension match
    [TestCase("DIR1", false, true)]   // case-insensitive, folders
    [TestCase("nope", true, true)]    // no matches
    public void FindName_MatchesStoreSearch(string pattern, bool files, bool folders)
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            var expected = new List<string>();
            EntryStoreSearch.Find(store, pattern, regexMode: false, includePath: false,
                includeFiles: files, includeFolders: folders, i => expected.Add(store.FullPath(i)));

            using var reader = new ColumnarCatalogReader(path);
            var actual = new List<string>();
            reader.FindName(pattern, files, folders, i => actual.Add(reader.FullPath(i)));

            Assert.That(actual.OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)));
        }
        finally { File.Delete(path); }
    }

    [TestCase(@"alpha\.txt", false)] // regex name
    [TestCase("beta", false)]
    [TestCase("alpha", true)]         // regex on full path
    public void Find_Regex_MatchesStoreSearch(string pattern, bool includePath)
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            var expected = new List<string>();
            EntryStoreSearch.Find(store, pattern, regexMode: true, includePath: includePath,
                includeFiles: true, includeFolders: true, i => expected.Add(store.FullPath(i)));

            using var reader = new ColumnarCatalogReader(path);
            var actual = new List<string>();
            reader.Find(pattern, regexMode: true, includePath: includePath,
                includeFiles: true, includeFolders: true, i => actual.Add(reader.FullPath(i)));

            Assert.That(actual.OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Find_FullFilter_SizeRange_MatchesStore()
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            var opts = new EntryStoreFindOptions
            {
                IncludeFiles = true,
                IncludeFolders = true,
                FromSizeEnable = true,
                FromSize = 1000, // only beta.log (5000) qualifies
            };

            var expected = new List<string>();
            EntryStoreSearch.Find(store, opts, i => expected.Add(store.FullPath(i)));

            using var reader = new ColumnarCatalogReader(path);
            var actual = new List<string>();
            reader.Find(opts, i => actual.Add(reader.FullPath(i)));

            // Reader and store must agree exactly (both include dir1, whose aggregated size >= 1000).
            Assert.That(actual.OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)));
            Assert.That(actual, Does.Contain(@"C:\test\dir1\beta.log"));
            Assert.That(actual, Does.Not.Contain(@"C:\test\root_file.txt")); // size 50, filtered out
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void CatalogTreeBuilder_RoundTrips_StructureSizesAndHashes()
    {
        // Set a hash on one file so the hash column round-trips too (the hash/dupes path).
        var root = BuildTree();
        var beta = root.Children.First(c => c.Path == "dir1").Children.First(c => c.Path == "beta.log");
        beta.SetHash([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        root.SetInMemoryFields();

        var store0 = EntryStore.Build(root);
        var path = WriteTemp(store0);
        try
        {
            // .cdex -> mutable tree (as hash/dupes do) -> store again must match the original store.
            var trees = CatalogTreeBuilder.FromColumnarFiles([path]);
            Assert.That(trees, Has.Count.EqualTo(1));
            var store2 = EntryStore.Build(trees[0]);

            Assert.That(store2.Count, Is.EqualTo(store0.Count));
            Assert.That(store2.HasHash, Is.True);
            for (var i = 0; i < store0.Count; i++)
            {
                Assert.That(store2.FullPath(i), Is.EqualTo(store0.FullPath(i)), $"path {i}");
                Assert.That(store2.Size[i], Is.EqualTo(store0.Size[i]), $"size {i}");
                Assert.That(store2.ModifiedTicks[i], Is.EqualTo(store0.ModifiedTicks[i]), $"modified {i}");
                Assert.That(store2.IsDirectory(i), Is.EqualTo(store0.IsDirectory(i)), $"isDir {i}");
                Assert.That(store2.IsHashDone(i), Is.EqualTo(store0.IsHashDone(i)), $"hashDone {i}");
                if (store0.IsHashDone(i))
                    Assert.That(store2.HashOf(i), Is.EqualTo(store0.HashOf(i)), $"hash {i}");
            }
        }
        finally { File.Delete(path); }
    }

    private static RootEntry BuildForCopy(bool withHash)
    {
        var root = new RootEntry { Path = @"C:\test" };
        var f = new DirEntry(false)
        {
            Path = "a.txt",
            Size = 100,
            Modified = new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc),
        };
        if (withHash) f.SetHash([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        root.AddChild(f);
        root.SetInMemoryFields();
        return root;
    }

    [Test]
    public void TraverseTreesCopyHash_FromReconstructedCdex_CopiesHashToFreshScan()
    {
        // Mirrors the re-scan hash-reuse path: old hashed catalog comes from a .cdex (reconstructed),
        // fresh scan tree has no hash; the hash must copy across.
        var store = EntryStore.Build(BuildForCopy(withHash: true));
        var path = WriteTemp(store);
        try
        {
            var oldRoot = CatalogTreeBuilder.FromColumnarFiles([path])[0];
            var fresh = BuildForCopy(withHash: false);

            oldRoot.TraverseTreesCopyHash(fresh);

            var file = fresh.Children.First(c => c.Path == "a.txt");
            Assert.That(file.IsHashDone, Is.True, "hash should have been copied from the reconstructed .cdex");
            Assert.That(file.Hash, Is.EqualTo(oldRoot.Children.First(c => c.Path == "a.txt").Hash));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void EntryRef_OverReader_NavigatesLikeStore()
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            using var reader = new ColumnarCatalogReader(path);
            // Same ICommonEntry adapter, backed by the mmap reader instead of the heap store.
            ICommonEntry rootRef = new EntryRef(reader, 0);
            ICommonEntry storeRootRef = new EntryRef(store, 0);

            Assert.That(rootRef.Children, Is.Not.Null);
            Assert.That(rootRef.Children.Count, Is.EqualTo(storeRootRef.Children.Count));
            Assert.That(rootRef.FullPath, Is.EqualTo(storeRootRef.FullPath));

            // Subtree counts (files/dirs) must match the heap-backed adapter.
            Assert.That(rootRef.FileEntryCount, Is.EqualTo(storeRootRef.FileEntryCount));
            Assert.That(rootRef.DirEntryCount, Is.EqualTo(storeRootRef.DirEntryCount));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void FindPath_MatchesStorePathSearch()
    {
        var store = EntryStore.Build(BuildTree());
        var path = WriteTemp(store);
        try
        {
            var expected = new List<string>();
            EntryStoreSearch.Find(store, "docs", regexMode: false, includePath: true,
                includeFiles: true, includeFolders: true, i => expected.Add(store.FullPath(i)));

            using var reader = new ColumnarCatalogReader(path);
            var actual = new List<string>();
            reader.FindPath("docs", includeFiles: true, includeFolders: true, i => actual.Add(reader.FullPath(i)));

            Assert.That(actual.OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)));
        }
        finally { File.Delete(path); }
    }
}
