using System;
using Caliburn.Micro;
using cdeLib;

namespace Finder.ViewModels
{
    public class StartViewModel : Screen
    {
        private string _name;
        private string _helloString;

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

        public string HelloString
        {
            get
            {
                return _helloString;
            }
            private set
            {
                _helloString = value;
                NotifyOfPropertyChange(() => HelloString);
            }
        }

        public bool CanSayHello
        {
            get { return !string.IsNullOrWhiteSpace(Name); }
        }

        public void LoadData()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            HelloString = string.Format("Data Loaded: {0}", rootEntries.Count);
        }

        public void Scan(string name)
        {
            //TODO: wire up command properly
            var re = new RootEntry();
            re.PopulateRoot(name);
            HelloString = String.Format("Filecount: {0}", re.FileCount);
        }

        public void SayHello(string name)
        {
            HelloString = string.Format("Hello {0}.", Name);
        }

        public void SearchScreen()
        {
            SearchViewModel model = new SearchViewModel();
            Screen screen = new Screen();
            WindowManager windowManager = new WindowManager();
            windowManager.ShowDialog(model);
        }
    }
}