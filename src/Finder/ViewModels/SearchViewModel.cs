using Caliburn.Micro;

namespace Finder.ViewModels
{
    public class SearchViewModel : PropertyChangedBase
    {
        private object _searchResults;

        public object SearchResults
        {
            get { return _searchResults; }
            set
            {
                _searchResults = value;
                NotifyOfPropertyChange(() => SearchResults);
            }
        }
    }
}