using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace cdeWeb.Models
{
    public interface IDirEntryRepository
    {
        //IEnumerable<DirEntry> Entries { get; }
        //IEnumerable<DirEntry> GetAll();
        //DirEntry Get(int id);
        IEnumerable<DirEntry> GetQuery(string query);
    }

    public class DirEntryRepository : IDirEntryRepository
    {
        private readonly IDataStore _dataStore;

        public DirEntryRepository(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        //public IEnumerable<DirEntry> Entries
        //{
        //    get
        //    {
        //        return _entries;
        //    }
        //}

        //public IEnumerable<DirEntry> GetAll()
        //{
        //    return _entries;
        //}

        //public DirEntry Get(int id)
        //{
        //    throw new HttpResponseException(HttpStatusCode.NotImplemented);
        //}

        public IEnumerable<DirEntry> GetQuery(string query)
        {
            var entry = _dataStore.Search(query).ToList();

            // ReSharper disable PossibleMultipleEnumeration
            if (!entry.Any())
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            return entry;
            // ReSharper restore PossibleMultipleEnumeration
        }
    }
}