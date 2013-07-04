using System.Collections.Generic;
using System.Linq;

namespace cdeWeb.Models
{
    public interface IDirEntryRepository
    {
        Results<DirEntry> GetQuery(string query);
    }

    public class DirEntryRepository : IDirEntryRepository
    {
        private readonly IDataStore _dataStore;

        public DirEntryRepository(IDataStore dataStore)
        {
            _dataStore = dataStore;
            _dataStore.LoadData(null);
        }

        public Results<DirEntry> GetQuery(string query)
        {
            return _dataStore.Search(query);
        }
    }
}