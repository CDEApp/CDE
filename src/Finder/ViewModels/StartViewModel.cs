using System;
using Caliburn.Micro;
using cdeLib;

namespace Finder.ViewModels
{
    public class StartViewModel : Screen
    {
        private string _name;
        private string _response;

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                NotifyOfPropertyChange(() => Name);
                NotifyOfPropertyChange(() => CanSayHello);
            }
        }

        public string Response
        {
            get
            {
                return _response;
            }
            private set
            {
                _response = value;
                NotifyOfPropertyChange(() => Response);
            }
        }

        public bool CanSayHello
        {
            get { return !string.IsNullOrWhiteSpace(Name); }
        }

        public void LoadData()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            Response = string.Format("Data Loaded: {0}", rootEntries.Count);
        }

        public void Scan(string name)
        {
            //TODO: wire up command properly
            var re = new RootEntry();
            re.PopulateRoot(name);
            Response = String.Format("Filecount: {0}", re.FileCount);
        }

        public void SayHello(string name)
        {
            Response = string.Format("Hello {0}.", Name);
        }

        public void ExecuteSearch()
        {
            //Activate a popup.
//            SearchViewModel model = new SearchViewModel();
//            Screen screen = new Screen();
//            WindowManager windowManager = new WindowManager();
//            windowManager.ShowDialog(model);

        }
    }
}