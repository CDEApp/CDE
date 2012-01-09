using System;
using Caliburn.Micro;
using cdeLib;

namespace Finder.ViewModels
{
    public class CathyViewModel : PropertyChangedBase, IShell
    {
        private string _name;
        private string _helloString;

        private BindableCollection<RootEntry> _rootEntries;
        public BindableCollection<RootEntry> RootEntries
        {
            get { return _rootEntries; }
            private set
            {
                _rootEntries = value;
                NotifyOfPropertyChange(() => RootEntries);
            }
        }

        private BindableCollection<RootEntry> _rootEntries2;
        public BindableCollection<RootEntry> RootEntries2 {
            get { return _rootEntries2; }
            private set
            {
                _rootEntries2 = value;
                NotifyOfPropertyChange(() => RootEntries2);
            }
        }

        private void LoadRoots()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            RootEntries = new BindableCollection<RootEntry>();
            RootEntries.AddRange(rootEntries);
            RootEntries2 = new BindableCollection<RootEntry>();
            RootEntries2.AddRange(rootEntries);
        }

        private BindableCollection<DirEntry> _foundEntries;
        public  BindableCollection<DirEntry> FoundEntries
        {
            get { return _foundEntries; }
            private set
            {
                _foundEntries = value;
                NotifyOfPropertyChange(() => FoundEntries);
            }
        }

        public void DoSearch()
        {
            Find.GetDirCache();
            Find.StaticFind("document", "--greppath");
        }

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
            get { return _helloString; }
            private set
            {
                _helloString = value;
                NotifyOfPropertyChange(()=>HelloString);
            }
        }

        public bool CanSayHello
        {
            get { return !string.IsNullOrWhiteSpace(Name); }
        }

        public void SayHello()
        {
            HelloString = string.Format("Hello {0}.", Name);
            LoadRoots();
        }

        //public bool CanSayHello(string name)
        //{
        //    return !string.IsNullOrWhiteSpace(name);
        //}

        //public void SayHello(string name)
        //{
        //    HelloString = string.Format("Hello {0}.", name);
        //}

        public void LoadData()
        {
            var rootEntries = RootEntry.LoadCurrentDirCache();
            HelloString = string.Format("Data Loaded: {0}",rootEntries.Count);
        }

        public void Scan(string name)
        {
            //TODO: wire up command properly
            var re = new RootEntry();
            re.PopulateRoot(name);
            HelloString = String.Format("Filecount: {0}", re.FileCount);
        }

    }
}