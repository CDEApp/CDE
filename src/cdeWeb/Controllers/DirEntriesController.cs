using System.Diagnostics;
using System.Linq;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
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

        [Queryable(PageSize=100,MaxTop=500)]
        public IQueryable<DirEntry> GetDirEntries(ODataQueryOptions<DirEntry> opts)
        {
            Debug.WriteLine(string.Format("Skip \"{0}\"", opts.RawValues.Skip));
            Debug.WriteLine(string.Format("Top \"{0}\"", opts.RawValues.Top));
            Debug.WriteLine(string.Format("Filter \"{0}\"", opts.RawValues.Filter));
            System.Threading.Thread.Sleep(1000);
            //return _dirEntryRepository.GetQuery(string.Empty).AsQueryable();
            return null;
        }
    }
}
