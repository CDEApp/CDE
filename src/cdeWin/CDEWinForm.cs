using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    /* Passive view hackery http://cre8ivethought.com/blog/2009/12/19/using-conventions-with-passive-view 
     * This is ok, but its one presenter per Form, lots of controls gets large...
     * Would like to break upinto seperate presenters for sets of controls and maybe a higher level
     * coordinating presenter.
     * 
     * Alternate presenter setup in cdeWinForm - need to review it again.
     */

    public interface ICDEWinForm : IView
    {
        event EventAction OnDirectoryTreeViewBeforeExpandNode;
        event EventAction OnDirectoryTreeViewAfterSelect;
        event EventAction OnMyFormClosing;
        event EventAction OnExitMenuItem;
        event EventAction OnSearch;
        event EventAction OnCancelSearch;
        
        event EventAction OnSearchResultContextMenuViewTreeClick;
        event EventAction OnSearchResultContextMenuOpenClick;
        event EventAction OnSearchResultContextMenuExploreClick;
        event EventAction OnSearchResultContextMenuPropertiesClick;
        event EventAction OnSearchResultContextMenuSelectAllClick;

        event EventAction OnDirectoryContextMenuViewTreeClick;
        event EventAction OnDirectoryContextMenuOpenClick;
        event EventAction OnDirectoryContextMenuExploreClick;
        event EventAction OnDirectoryContextMenuPropertiesClick;
        event EventAction OnDirectoryContextMenuSelectAllClick;
        event EventAction OnDirectoryContextMenuParentClick;

        event EventAction OnDirectoryRetrieveVirtualItem;
        void OnDirectoryRetrieveVirtualItemFire();
        event EventAction OnDirectoryListViewItemActivate;
        void OnDirectoryListViewItemActivateFire();
        event EventAction OnDirectoryListViewColumnClick;
        void OnDirectoryListViewColumnClickFire();
        event EventAction OnDirectoryListViewItemSelectionChanged;
        void OnDirectoryListViewItemSelectionChangedFire();

        event EventAction OnSearchResultRetrieveVirtualItem;
        void OnSearchResultRetrieveVirtualItemFire();
        event EventAction OnSearchResultListViewItemActivate;
        void OnSearchResultListViewItemActivateFire();
        event EventAction OnSearchResultListViewColumnClick;
        void OnSearchResultListViewColumnClickFire();

        event EventAction OnCatalogRetrieveVirtualItem;
        void OnCatalogRetrieveVirtualItemFire();
        event EventAction OnCatalogListViewItemActivate;
        void OnCatalogListViewItemActivateFire();
        event EventAction OnCatalogListViewColumnClick;
        void OnCatalogListViewColumnClickFire();

        TreeNode DirectoryTreeViewNodes { get;  set; }

        TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        string Pattern { get; set; }
        bool RegexMode { get; set; }
        bool IncludeFiles { get; }
        bool IncludeFolders { get; }
        int FindEntryFilter { get; set; }
        bool IncludePathInSearch { get; set; }

        void SetSearchTextBoxAutoComplete(IEnumerable<string> history);
        void AddSearchTextBoxAutoComplete(string pattern);
        List<string> GetSearchTextBoxAutoComplete();
        void SelectDirectoryPane();
        float DirectoryPanelSplitterRatio { get; set; }
        string SetDirectoryPathTextbox { set; }
        TreeNode SetDirectoryTreeViewSelectedNode { set; }
        void SetSearchResultStatus(int i);
        void SetCatalogsLoadedStatus(int i);
        void SetSearchTimeStatus(string s);
        bool SearchButtonEnable { get; set; }
        bool CancelSearchButtonEnable { get; set; }

        ListViewHelper<PairDirEntry> SearchResultListViewHelper { get; set; }
        ListViewHelper<DirEntry> DirectoryListViewHelper { get; set; }
        ListViewHelper<RootEntry> CatalogListViewHelper { get; set; }

        //void DirectoryListViewDeselectItems();
        //void SelectDirectoryListViewItem(int index);
    }

    public partial class CDEWinForm : Form, ICDEWinForm
    {
        private const int DirectoryTabIndex = 2;

        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearch;
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogRetrieveVirtualItem;
        public event EventAction OnExitMenuItem;
        public event EventAction OnCancelSearch;

        public event EventAction OnSearchResultContextMenuViewTreeClick;
        public event EventAction OnSearchResultContextMenuOpenClick;
        public event EventAction OnSearchResultContextMenuExploreClick;
        public event EventAction OnSearchResultContextMenuPropertiesClick;
        public event EventAction OnSearchResultContextMenuSelectAllClick;

        public event EventAction OnDirectoryContextMenuViewTreeClick;
        public event EventAction OnDirectoryContextMenuOpenClick;
        public event EventAction OnDirectoryContextMenuExploreClick;
        public event EventAction OnDirectoryContextMenuPropertiesClick;
        public event EventAction OnDirectoryContextMenuSelectAllClick;
        public event EventAction OnDirectoryContextMenuParentClick;

        public event EventAction OnDirectoryRetrieveVirtualItem;
        public virtual void OnDirectoryRetrieveVirtualItemFire() { OnDirectoryRetrieveVirtualItem(); }
        public event EventAction OnDirectoryListViewItemActivate;
        public virtual void OnDirectoryListViewItemActivateFire() { OnDirectoryListViewItemActivate(); }
        public event EventAction OnDirectoryListViewColumnClick;
        public virtual void OnDirectoryListViewColumnClickFire() { OnDirectoryListViewColumnClick(); }
        public event EventAction OnDirectoryListViewItemSelectionChanged;
        public virtual void OnDirectoryListViewItemSelectionChangedFire() { OnDirectoryListViewItemSelectionChanged(); }

        public event EventAction OnSearchResultRetrieveVirtualItem;
        public virtual void OnSearchResultRetrieveVirtualItemFire() { OnSearchResultRetrieveVirtualItem(); }
        public event EventAction OnSearchResultListViewItemActivate;
        public virtual void OnSearchResultListViewItemActivateFire() { OnSearchResultListViewItemActivate(); }
        public event EventAction OnSearchResultListViewColumnClick;
        public virtual void OnSearchResultListViewColumnClickFire() { OnSearchResultListViewColumnClick(); }

        public virtual void OnCatalogRetrieveVirtualItemFire() { OnCatalogRetrieveVirtualItem(); }
        public event EventAction OnCatalogListViewItemActivate;
        public virtual void OnCatalogListViewItemActivateFire() { OnCatalogListViewItemActivate(); }
        public event EventAction OnCatalogListViewColumnClick;
        public void OnCatalogListViewColumnClickFire() { OnCatalogListViewColumnClick(); }

        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        public ListViewHelper<PairDirEntry> SearchResultListViewHelper { get; set; }
        public ListViewHelper<DirEntry> DirectoryListViewHelper { get; set; }
        public ListViewHelper<RootEntry> CatalogListViewHelper { get; set; }

        public CDEWinForm()
        {
            InitializeComponent();
            AutoWaitCursor.Cursor = Cursors.WaitCursor;
            AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
            AutoWaitCursor.MainWindowHandle = Handle;
            AutoWaitCursor.Start();
            RegisterClientEvents();
        }

        private void RegisterClientEvents()
        {
            FormClosing += (s, e) => OnMyFormClosing();
            Activated += MyFormActivated;

            whatToSearchComboBox.Items.AddRange(new[] { "Include Path in Search", "Exclude Path from Search" });
            //whatToSearchComboBox.Items.AddRange(new[] { "Include", "Exclude" });
            whatToSearchComboBox.SelectedIndex = 0; // default Include
            whatToSearchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

            findComboBox.Items.AddRange(new[] { "Files and Folders", "Files Only", "Folders Only" });
            findComboBox.SelectedIndex = 0; // default Files and Folders
            findComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

            SearchResultListViewHelper = new ListViewHelper<PairDirEntry>(searchResultListView)
                {
                    RetrieveVirtualItem = OnSearchResultRetrieveVirtualItemFire,
                    ItemActivate = OnSearchResultListViewItemActivateFire,
                    ColumnClick = OnSearchResultListViewColumnClickFire,
                    ContextMenu = CreateSearchResultContextMenu(),
                };

            DirectoryListViewHelper = new ListViewHelper<DirEntry>(directoryListView)
                {
                    RetrieveVirtualItem = OnDirectoryRetrieveVirtualItemFire,
                    ItemActivate = OnDirectoryListViewItemActivateFire,
                    ColumnClick = OnDirectoryListViewColumnClickFire,
                    ContextMenu = CreateDirectoryContextMenu(),
                    ItemSelectionChanged = OnDirectoryListViewItemSelectionChangedFire
                };

            directoryTreeView.BeforeExpand += DirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect += DirectoryTreeViewOnAfterSelect;

            // Enter in pattern Text Box fires Search Button.
            patternComboBox.GotFocus += (s, e) => AcceptButton = searchButton;
            patternComboBox.LostFocus += (s, e) => AcceptButton = null;
            patternComboBox.DropDownHeight = 1;

            CatalogListViewHelper = new ListViewHelper<RootEntry>(catalogResultListView)
                {
                    MultiSelect = false,
                    RetrieveVirtualItem = OnCatalogRetrieveVirtualItemFire,
                    ItemActivate = OnCatalogListViewItemActivateFire,
                    ColumnClick = OnCatalogListViewColumnClickFire,
                };

            directoryPathTextBox.ReadOnly = true; // only for display and manual select copy for now ?

            exitToolStripMenuItem.Click += (s, e) => OnExitMenuItem();

            searchButton.Click += (s, e) => OnSearch();
            cancelSearchButton.Click +=  (s, e) => OnCancelSearch();
            var cancelSearchTooltip = new ToolTip();
            cancelSearchTooltip.SetToolTip(cancelSearchButton, "Cancel Search is not immediate, wait for a progress update.");
            CancelSearchButtonEnable = false;
        }

        private ContextMenuStrip CreateDirectoryContextMenu()
        {
            var menuHelper = new ContextMenuHelper
                {
                    TreeViewHandler = (s, e) => OnDirectoryContextMenuViewTreeClick(),
                    OpenHandler = (s, e) => OnDirectoryContextMenuOpenClick(),
                    ExploreHandler = (s, e) => OnDirectoryContextMenuExploreClick(),
                    PropertiesHandler = (s, e) => OnDirectoryContextMenuPropertiesClick(),
                    SelectAllHandler = (s, e) => OnDirectoryContextMenuSelectAllClick(),
                    //CopyBaseNameHandler = (s, e) => (),
                    //CopyFullNameHandler = (s, e) => (),
                    ParentHandler = (s, e) => OnDirectoryContextMenuParentClick(),
                };
            return menuHelper.GetContextMenuStrip();
        }

        private ContextMenuStrip CreateSearchResultContextMenu()
        {
            var menuHelper = new ContextMenuHelper
            {
                TreeViewHandler = (s,e) => OnSearchResultContextMenuViewTreeClick(),
                OpenHandler = (s, e) => OnSearchResultContextMenuOpenClick(),
                ExploreHandler = (s, e) => OnSearchResultContextMenuExploreClick(),
                PropertiesHandler = (s, e) => OnSearchResultContextMenuPropertiesClick(),
                SelectAllHandler = (s, e) => OnSearchResultContextMenuSelectAllClick(),
                //CopyBaseNameHandler = (s, e) => (),
                //CopyFullNameHandler = (s, e) => (),
            };
            return menuHelper.GetContextMenuStrip();
        }

        private void MyFormActivated(object sender, EventArgs eventArgs)
        {
            patternComboBox.Focus();
        }

        private void DirectoryTreeViewOnAfterSelect(object s, TreeViewEventArgs e)
        {
            DirectoryTreeViewActiveAfterSelectNode = e.Node;
            OnDirectoryTreeViewAfterSelect();
        }

        private void DirectoryTreeViewOnBeforeExpand(object s, TreeViewCancelEventArgs e)
        {
            DirectoryTreeViewActiveBeforeExpandNode = e.Node;
            OnDirectoryTreeViewBeforeExpandNode();
        }

        public string Pattern
        {
            get { return patternComboBox.Text; }
            set { patternComboBox.Text = value; }
        }

        public bool RegexMode
        {
            get { return regexCheckbox.Checked; }
            set { regexCheckbox.Checked = value; }
        }

        /// <summary>
        /// Adds nodes to tree wrapped by BeginUpdate EndUpdate.
        /// </summary>
        public TreeNode DirectoryTreeViewNodes
        {
            get
            {   // we only use one root node at the moment.
                return directoryTreeView.Nodes.Count == 1 ? directoryTreeView.Nodes[0] : null;
            }

            set
            {
                if (value != null)
                {
                    directoryTreeView.BeginUpdate();
                    directoryTreeView.Nodes.Clear();
                    directoryTreeView.Nodes.Add(value);
                    directoryTreeView.SelectedNode = directoryTreeView.Nodes[0];
                    directoryTreeView.Nodes[0].Expand();
                    directoryTreeView.Select();
                    directoryTreeView.EndUpdate();
                }
            }
        }

        public bool IncludeFiles
        {
            get { return findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 1; }
        }

        public bool IncludeFolders
        {
            get { return findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 2; }
        }

        public int FindEntryFilter
        {
            get { return findComboBox.SelectedIndex; }
            set { findComboBox.SelectedIndex = value; }
        }

        public bool IncludePathInSearch
        {
            get { return whatToSearchComboBox.SelectedIndex == 0; }
            set { whatToSearchComboBox.SelectedIndex = value ? 0 : 1; }
        }

        public void SetSearchResultStatus(int i)
        {
            searchResultsStatus.Text = "SR " + i.ToString();
        }

        public void SetCatalogsLoadedStatus(int i)
        {
            catalogsLoadedStatus.Text = "C " + i.ToString();
        }

        public void SetSearchTimeStatus(string s)
        {
            searchTimeStatus.Text = s;
        }

        public void SetSearchTextBoxAutoComplete(IEnumerable<string> history)
        {
            patternComboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            patternComboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            patternComboBox.AutoCompleteCustomSource = history.ToAutoCompleteStringCollection();
        }

        public void AddSearchTextBoxAutoComplete(string pattern)
        {
            var ac = patternComboBox.AutoCompleteCustomSource;
            if (!ac.Contains(pattern))
            {
                ac.Add(pattern);
            }
        }

        public List<string> GetSearchTextBoxAutoComplete()
        {
            var list = new List<string>(20);
            list.AddRange(patternComboBox.AutoCompleteCustomSource.Cast<string>());
            return list;
        }

        public void SelectDirectoryPane()
        {
            // ReSharper disable RedundantCheckBeforeAssignment
            if (mainTabControl.SelectedIndex != DirectoryTabIndex)
            {
                mainTabControl.SelectedIndex = DirectoryTabIndex;
            }
            // ReSharper restore RedundantCheckBeforeAssignment
        }

        public float DirectoryPanelSplitterRatio
        { 
            get { return directorySplitContainer.GetSplitterRatio(); }
            set { directorySplitContainer.SetSplitterRatio(value); } 
        }

        public string SetDirectoryPathTextbox
        {
            set { directoryPathTextBox.Text = value; }
        }

        public TreeNode SetDirectoryTreeViewSelectedNode
        {
            set { directoryTreeView.SelectedNode = value; }
        }

        public bool SearchButtonEnable
        {
            get { return searchButton.Enabled; }
            set { searchButton.Enabled = value; }
        }

        public bool CancelSearchButtonEnable
        {
            get { return cancelSearchButton.Enabled; }
            set { cancelSearchButton.Enabled = value; }
        }
    }
}
