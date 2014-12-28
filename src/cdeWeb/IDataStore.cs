using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Util;
using cdeLib;
using DirEntry = cdeWeb.Models.DirEntry;

namespace cdeWeb
{
    public interface IDataStore
    {
        TimeIt Metric { get; }
        void LoadDataEnsureOnce(Func<int, bool> notifyCount);
        void LoadData(Func<int, bool> notifyCount);
        //int TotalFileEntries();
        Results<DirEntry> Search(string search, Action<int, int> progress = null);
        Results<DirEntry> Search(string search, Action<int, int> progress, Action<DirEntry> found);
    }

    public class DataStore : IDataStore
    {
        private readonly string _basePath;
        private List<RootEntry> _rootEntries;
        private readonly TimeIt _loadMetric;
        private bool _reportedLoadMetric;

        private object _dataLoadedFlagObject;
        private object _dataStoreSyncLock;
        private bool _dataStoreInitialized;
        private Func<int, bool> _notifyCount = delegate { return true; };

        public TimeIt Metric { get { return _loadMetric; } }

        public DataStore()
        {
            _reportedLoadMetric = false;
            _loadMetric = new TimeIt();
        }

        public DataStore(string basePath): this()
        {
            _basePath = basePath;
            //LoadData(basePath);
        }

        /// <summary>
        /// Ensure only 1 caller causes data to load.
        /// </summary>
        public void LoadDataEnsureOnce(Func<int, bool> notifyCount)
        {
            if (notifyCount != null)
            {
                _notifyCount = notifyCount;
            }
            LazyInitializer.EnsureInitialized(
                ref _dataLoadedFlagObject, ref _dataStoreInitialized,
                ref _dataStoreSyncLock, LoadDataWithNotifyCount);
        }

        private IDataStore LoadDataWithNotifyCount()
        {
            Debug.WriteLine(string.Format("LoadData ------ "));
            LoadData(_notifyCount);
            return this;
        }

        public void LoadData(Func<int, bool> notifyCount)
        {
            var paths = new[] { _basePath };
            var cacheFiles = RootEntry.GetCacheFileList(paths);
            var cacheFileCount = cacheFiles.Count;

            _loadMetric.Start("Loading catalogs");
            _notifyCount(cacheFileCount);
            _rootEntries = cacheFiles.Select(s =>
			{
				var re = RootEntry.LoadDirCache(s);
			    --cacheFileCount;
                _notifyCount(cacheFileCount);
			    return re;
			}).ToList();
            _loadMetric.Stop();
        }

        //public int TotalFileEntries()
        //{
        //    return _rootEntries.TotalFileEntries();
        //}

        //public IEnumerable<Models.DirEntry> Search(string search)
        public Results<DirEntry> Search(string search, Action<int, int> progress = null)
        {
            var results = ReportLoadMetricOnce();

            var findOptions = new FindOptions
            {
                Pattern = search,
                RegexMode = false,
                IncludePath = true,
                IncludeFiles = true,
                IncludeFolders = true,
                LimitResultCount = 25,
                SkipCount = 0,
                ProgressFunc = progress,
                ProgressModifier = 5000,
                VisitorFunc = (p, d) =>
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

        public Results<DirEntry> Search(string search, Action<int, int> progress, Action<DirEntry> found)
        {
            var results = ReportLoadMetricOnce();

            var findOptions = new FindOptions
            {
                Pattern = search,
                RegexMode = false,
                IncludePath = true,
                IncludeFiles = true,
                IncludeFolders = true,
                LimitResultCount = 25,
                SkipCount = 0,
                ProgressFunc = progress,
                ProgressModifier = 5000,
                VisitorFunc = (p, d) =>
                {
                    found(new DirEntry(p, d));
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