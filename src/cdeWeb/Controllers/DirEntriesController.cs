using System.Linq;
using System.Web.Http;
using System.Web.Http.OData;
using cdeWeb.Models;

namespace cdeWeb.Controllers
{
    public class DirEntriesController : ODataController
    {
        private readonly IDirEntryRepository _dirEntryRepository;

        public DirEntriesController(IDirEntryRepository dirEntryRepository)
        {
            _dirEntryRepository = dirEntryRepository;
        }

        [Queryable(PageSize=4,MaxTop=100)]
        public IQueryable<DirEntry> GetDirEntries()
        {
            return _dirEntryRepository.Entries.AsQueryable();
        }
    }
}
