using System;
using System.Collections.Generic;
using System.IO;
using cdeAppCore;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NSubstitute;
using Serilog;

namespace cdeAppCoreTest;

/// <summary>
/// Builds a small fixture catalog, writes it to a temp <c>.cdex</c>, and loads it into a real
/// <see cref="CatalogSession"/> over a stubbed loader. Disposing releases the mmap then deletes the
/// file. Tree shape:
/// <code>
///   C:\test
///   ├─ dir1\        (dir)
///   │   ├─ alpha.txt
///   │   └─ beta.log
///   ├─ docs\        (dir)
///   │   └─ alpha.md
///   └─ root_file.txt
/// </code>
/// </summary>
internal sealed class CatalogFixture : IDisposable
{
    public string CdexPath { get; }
    public CatalogSession Session { get; }

    public CatalogFixture()
    {
        var store = EntryStore.Build(BuildTree());
        CdexPath = Path.Combine(Path.GetTempPath(), $"cdeappcoretest-{Guid.NewGuid():N}.cdex");
        ColumnarFormat.Write(store, CdexPath);

        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns(new List<string> { CdexPath });

        Session = new CatalogSession(loader, Substitute.For<ILogger>());
        Session.LoadAsync("config").GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Session.Dispose(); // release the memory map before deleting the file
        try { File.Delete(CdexPath); }
        catch { /* best effort */ }
    }

    private static RootEntry BuildTree()
    {
        var root = new RootEntry
        {
            Path = @"C:\test",
            VolumeName = "VOL",
            DefaultFileName = "test.cde",
            ActualFileName = "test.cde",
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
}
