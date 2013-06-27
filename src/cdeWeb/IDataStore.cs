using System.Collections.Generic;
using Util;
using cdeLib;
using DirEntry = cdeWeb.Models.DirEntry;

namespace cdeWeb
{
    public interface IDataStore
    {
        Results<DirEntry> Search(string search);
    }

    public class DataStore : IDataStore
    {
        private List<RootEntry> _rootEntries;
        private readonly TimeIt _loadMetric;
        private bool _reportedLoadMetric;

        public DataStore()
        {
            _reportedLoadMetric = false;
            _loadMetric = new TimeIt();
            _loadMetric.Start("Loading catalogs");
        }

        public DataStore(string basePath): this()
        {
            LoadData(basePath);
        }

        void LoadData(string basepath)
        {
            var paths = new[] { basepath };
            var a = RootEntry.GetCacheFileList(paths);
            _rootEntries = RootEntry.Load(a);
            _loadMetric.Stop();
        }

        //public IEnumerable<Models.DirEntry> Search(string search)
        public Results<DirEntry> Search(string search)
        {
            var results = ReportLoadMetricOnce();

            var findOptions = new FindOptions
            {
                Pattern = search,
                RegexMode = false,
                IncludePath = true,
                IncludeFiles = true,
                IncludeFolders = true,
                LimitResultCount = 50,// int.MaxValue, // TODO skip X and get 50 ? doent exist yet in Find.
                FoundFunc = (p, d) =>
                {
                    results.Value.Add(new DirEntry(p, d));
                    //return list.Count < 25; // dont need to limit count here feature field above for it.
                    return true;
                },
            };

            results.Start("Find");
            findOptions.Find(_rootEntries);
            results.Stop();
            return results;
        }

        private Results<DirEntry> ReportLoadMetricOnce()
        {
            var results = new Results<DirEntry>(!_reportedLoadMetric ? _loadMetric.ElapsedList : null);
            _reportedLoadMetric = true;
            return results;
        }
    }

    public class Results<T>: TimeIt
    {
        public IList<T> Value;
        public string NextUri = "";

        public Results(IEnumerable<LabelElapsed> prefix)
        {
            Value = new List<T>();
            if (prefix != null)
            {
                _elapsedList.AddRange(prefix);
            }
        }

    }
}