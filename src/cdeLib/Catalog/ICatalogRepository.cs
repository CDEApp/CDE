using System.Collections.Generic;
using System.Threading.Tasks;
using cdeLib.Entities;

namespace cdeLib.Catalog;

public interface ICatalogRepository
{
    RootEntry Read(string fileName);
    IList<RootEntry> Load(IList<string> cdeList);
    Task<IList<RootEntry>> LoadAsync(IList<string> cdeList);
    IList<RootEntry> LoadCurrentDirCache();
    Task Save(RootEntry rootEntry);

    /// <summary>
    /// This gets .cde files in the current dir or one directory down.
    /// Use directory permissions to control who can load what .cde files one dir down if you like.
    /// </summary>
    IList<string> GetCacheFileList(IEnumerable<string> paths);

    /// <summary>
    /// Gets columnar <c>.cdex</c> catalogs in the current dir or one directory down — the zero-copy
    /// mmap format produced by <c>cde migrate</c>. Mirrors <see cref="GetCacheFileList"/> for .cde.
    /// </summary>
    IList<string> GetColumnarFileList(IEnumerable<string> paths);

    RootEntry LoadDirCache(string file);
}