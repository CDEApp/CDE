using System.Collections.Generic;
using Caliburn.Micro;

namespace Finder.ViewModels
{
    public class ResultsViewModel : PropertyChangedBase
    {
        public IObservableCollection<IndividualResultViewModel> Results { get; private set; }

        public ResultsViewModel With(IEnumerable<SearchResults> searchResults)
        {
            Results.Clear();
            int number = 1;
            return new ResultsViewModel();
        }

    }
}