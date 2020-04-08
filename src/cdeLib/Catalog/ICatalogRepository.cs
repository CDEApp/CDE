using System.Collections.Generic;
using System.Threading.Tasks;

namespace cdeLib.Catalog
{
    public interface ICatalogRepository
    {
        RootEntry Read(string fileName);
        IList<RootEntry> Load(IEnumerable<string> cdeList);
        IList<RootEntry> LoadCurrentDirCache();
        Task Save(RootEntry rootEntry);

        /// <summary>
        /// This gets .cde files in current dir or one directory down.
        /// Use directory permissions to control who can load what .cde files one dir down if you like.
        /// </summary>
        IList<string> GetCacheFileList(IEnumerable<string> paths);

        RootEntry LoadDirCache(string file);
    }
}