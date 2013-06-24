using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Query;
using Microsoft.Data.Edm;
using cdeWeb.Models;

namespace cdeWeb.Controllers
{
    public class SearchController : ApiController
    {
        private static IDirEntryRepository _dirEntryRepository;// = new DirEntryRepository();

        public SearchController(IDirEntryRepository dirEntryRepository) // adding parameter CatalogRepository later.
        {
            _dirEntryRepository = dirEntryRepository;
        }

        // todo that restful api documentation thingy gruf mentioned ? ? 
        // todo think about odata early
        // todo add paging 

        //public IEnumerable<DirEntry> GetAll()
        //{
        //    return _dirEntryRepository.GetAll();
        //}

        public DirEntry GetDirEntry(int id)
        {
            throw new HttpResponseException(HttpStatusCode.NotImplemented);
            //var item = repository.Get(id);
            //if (item == null)
            //{
            //    throw new HttpResponseException(HttpStatusCode.NotFound);
            //}
            //return item;
        }

        public IEnumerable<DirEntry> GetProductsByCategory(string query)
        {
            return _dirEntryRepository.GetQuery(query);
            //.GetAll()
            //.Where(d => string.Equals(d.Name, query, StringComparison.OrdinalIgnoreCase));
        }

        // This works but data source isnt odata, so dont get extra nice rest bits. try next.
        //public IQueryable<DirEntry> Get(ODataQueryOptions<DirEntry> opts)
        //{
        //    System.Diagnostics.Debug.WriteLine("EEE BY GUM....");
        //    var results = opts.ApplyTo(_dirEntryRepository
        //                                   .GetAll()
        //                                   .AsQueryable());
        //    return results as IQueryable<DirEntry>;
        //}


        // http://blogs.msdn.com/b/webdev/archive/2013/01/29/getting-started-with-asp-net-webapi-odata-in-3-simple-steps.aspx


        public PageResult<DirEntry> Get(ODataQueryOptions<DirEntry> opts)
        {
            System.Diagnostics.Debug.WriteLine("EEE BY GUM....");

            var t = new ODataValidationSettings() { MaxTop = 25 };
            opts.Validate(t);

            var settings = new ODataQuerySettings()
            {
                PageSize = 2
            };

            var results = opts.ApplyTo(_dirEntryRepository
                                           .GetQuery(string.Empty)
                                           .AsQueryable(), settings);
            return new PageResult<DirEntry>(results as IEnumerable<DirEntry>,
                                            Request.GetNextPageLink(), // null ?
                                            Request.GetInlineCount()); // null ?

        }


    }
}
