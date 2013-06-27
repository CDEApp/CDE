using System.Web.Http;
using cdeWeb.Models;

namespace cdeWeb.Controllers
{
    public class SearchController : ApiController
    {
        private static IDirEntryRepository _dirEntryRepository;

        public SearchController(IDirEntryRepository dirEntryRepository)
        {
            _dirEntryRepository = dirEntryRepository;
        }

        public Results<DirEntry> Get(string query = "")
        {
            return _dirEntryRepository.GetQuery(query);
        }

        // todo look at swagger.net for documenting restful service.
        // todo add paging 
    }
}
