using System.Collections.Generic;
using cdeLib;

namespace cdeWeb
{
    public interface IDataStore
    {
        IEnumerable<Models.DirEntry> Search(string search);
    }

    public class DataStore : IDataStore
    {
        private List<RootEntry> _rootEntries = null;

        public DataStore()
        {
        }

        public DataStore(string basePath)
        {
            LoadData(basePath);
        }

        void LoadData(string basepath)
        {
            var paths = new[] { basepath };
            var a = RootEntry.GetCacheFileList(paths);
            _rootEntries = RootEntry.Load(a);
        }

        public IEnumerable<Models.DirEntry> Search(string search)
        {
            var list = new List<Models.DirEntry>();

            var findOptions = new FindOptions
            {
                Pattern = search,
                RegexMode = false,
                IncludePath = true,
                IncludeFiles = true,
                IncludeFolders = true,
                LimitResultCount = 5000000,
                FoundFunc = (p, d) =>
                {
                    list.Add(new Models.DirEntry(p, d));
                    return true;
                },
            };
            findOptions.Find(_rootEntries);
            return list;
        }
    }
}