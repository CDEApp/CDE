using System;
using System.Diagnostics;
using Microsoft.AspNet.SignalR;
using cdeWeb.Models;

namespace cdeWeb.Hubs
{

    public class ServerTimeHub : Hub
    {
        private static int count = 1;

        public string GetServerTime()
        {
            return string.Format("{0} {1}", DateTime.UtcNow.ToString(), count++);
        }
    }

    public class ClientPushHub : Hub
    {
    }

    public class SearchHub : Hub
    {
        private IDataStore _dataStore;
        //private bool _dataStoreInitialized = false;
        //private object _dataStoreSyncLock = null;
        //private object _dummyInitialized = null;

        private readonly IHubContext _hub;

        public void DoIt(int i)
        {

        }

        public SearchHub(IDataStore dataStore)
        {
            _dataStore = dataStore;
            _hub = GlobalHost.ConnectionManager.GetHubContext<SearchHub>();

            // load catalogs -- ensure no concurrency issue ? by using autofac single instance
            // capture total run time, and send events to hub for each loaded file.
            //var catalogFiles = RootEntry.GetCacheFileList(paths);
        }

        public int Search(string pattern)
        {
            _dataStore.LoadDataEnsureOnce(NotifyLoadFileCount);
            Debug.WriteLine(string.Format("Query parameters: \"{0}\"", pattern));
            NotifySearchStart();
            _dataStore.Search(pattern, NotifySearchProgress, AddDirEntry);
            NotifySearchDone();
            return 7;
        }

        private void AddDirEntry(DirEntry de)
        {
            _hub.Clients.Client(Context.ConnectionId).addDirEntry(de);
        }

        private bool NotifyLoadFileCount(int count)
        {
            _hub.Clients.Client(Context.ConnectionId).filesToLoad(count);
            return true;
        }

        private void NotifySearchProgress(int current, int progressEnd)
        {
            _hub.Clients.Client(Context.ConnectionId).searchProgress(current, progressEnd);
            //return true;
        }

        private void NotifySearchStart()
        {
            _hub.Clients.Client(Context.ConnectionId).searchStart();
        }

        private void NotifySearchDone()
        {
            _hub.Clients.Client(Context.ConnectionId).searchDone();
        }

    }
}