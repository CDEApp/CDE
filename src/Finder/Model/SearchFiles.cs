using System.Collections.Generic;

namespace Finder.Model
{
    public class SearchFiles : IQuery<IEnumerable<SearchResult>> 
    {
        public string SearchText { get; set; }
    }
}