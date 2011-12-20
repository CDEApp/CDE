using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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
        event EventAction OnSearchRoots;
        event EventAction OnSearchResultRetrieveVirtualItem;
        void OnSearchResultRetrieveVirtualItemFire();
        event EventAction OnDirectoryRetrieveVirtualItem;
        void OnDirectoryRetrieveVirtualItemFire();
        event EventAction OnCatalogRetrieveVirtualItem;
        void OnCatalogRetrieveVirtualItemFire();
        event EventAction OnMyFormClosing;
        event EventAction OnCatalogListViewItemActivate;
        void OnCatalogListViewItemActivateFire();
        event EventAction OnDirectoryListViewItemActivate;
        void OnDirectoryListViewItemActivateFire();
        event EventAction OnSearchResultListViewItemActivate;
        void OnSearchResultListViewItemActivateFire();
        event EventAction OnDirectoryListViewItemSelectionChanged;
        void OnDirectoryListViewItemSelectionChangedFire();
        event EventAction OnSearchResultListViewColumnClick;
        void OnSearchResultListViewColumnClickFire();
        event EventAction OnDirectoryListViewColumnClick;
        void OnDirectoryListViewColumnClickFire();
        event EventAction OnExitMenuItem;
        event EventAction OnSearchResultContextMenuExploreClick;

        TreeNode DirectoryTreeViewNodes { get;  set; }

        TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        string Pattern { get; set; }
        bool RegexMode { get; set; }
        bool IncludePathInSearch { get; }

        void SetSearchTextBoxAutoComplete(IEnumerable<string> history);
        void AddSearchTextBoxAutoComplete(string pattern);
        List<string> GetSearchTextBoxAutoComplete();
        void SelectDirectoryPane();
        float DirectoryPanelSplitterRatio { get; set; }
        string SetDirectoryPathTextbox { set; }
        TreeNode SetDirectoryTreeViewSelectedNode { set; }
        void SetSearchResultStatus(int i);
        void SetCatalogsLoadedStatus(int i);
        bool SearchButtonEnable { get; set; }

        ListViewHelper SearchResultListViewHelper { get; set; }
        ListViewHelper DirectoryListViewHelper { get; set; }
        ListViewHelper CatalogListViewHelper { get; set; }
        //void DirectoryListViewDeselectItems();
        //void SelectDirectoryListViewItem(int index);
    }

    public partial class CDEWinForm : Form, ICDEWinForm
    {
        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearchRoots;
        public event EventAction OnSearchResultRetrieveVirtualItem;
        public virtual void OnSearchResultRetrieveVirtualItemFire() { OnSearchResultRetrieveVirtualItem(); }
        public event EventAction OnDirectoryRetrieveVirtualItem;
        public virtual void OnDirectoryRetrieveVirtualItemFire() { OnDirectoryRetrieveVirtualItem(); }
        public event EventAction OnCatalogRetrieveVirtualItem;
        public virtual void OnCatalogRetrieveVirtualItemFire() { OnCatalogRetrieveVirtualItem(); }
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogListViewItemActivate;
        public virtual void OnCatalogListViewItemActivateFire() { OnCatalogListViewItemActivate(); }
        public event EventAction OnDirectoryListViewItemActivate;
        public virtual void OnDirectoryListViewItemActivateFire() { OnDirectoryListViewItemActivate(); }
        public event EventAction OnSearchResultListViewItemActivate;
        public virtual void OnSearchResultListViewItemActivateFire() { OnSearchResultListViewItemActivate(); }
        public event EventAction OnDirectoryListViewItemSelectionChanged;
        public virtual void OnDirectoryListViewItemSelectionChangedFire() { OnDirectoryListViewItemSelectionChanged(); }
        public event EventAction OnSearchResultListViewColumnClick;
        public virtual void OnSearchResultListViewColumnClickFire() { OnSearchResultListViewColumnClick(); }
        public event EventAction OnDirectoryListViewColumnClick;
        public virtual void OnDirectoryListViewColumnClickFire() { OnDirectoryListViewColumnClick(); }
        public event EventAction OnExitMenuItem;
        public event EventAction OnSearchResultContextMenuExploreClick;

        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        public ListViewHelper SearchResultListViewHelper { get; set; }
        public ListViewHelper DirectoryListViewHelper { get; set; }
        public ListViewHelper CatalogListViewHelper { get; set; }

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

            whatToSearchComboBox.Items.AddRange(new[] {"Include","Exclude"});
            whatToSearchComboBox.SelectedIndex = 0; // default Include

            SearchResultListViewHelper = new ListViewHelper(searchResultListView)
                {
                    RetrieveVirtualItem = OnSearchResultRetrieveVirtualItemFire,
                    ItemActivate = OnSearchResultListViewItemActivateFire,
                    ColumnClick = OnSearchResultListViewColumnClickFire,
                    ContextMenu = CreateSearchResultContextMenu(),
                };

            DirectoryListViewHelper = new ListViewHelper(directoryListView)
                {
                    RetrieveVirtualItem = OnDirectoryRetrieveVirtualItemFire,
                    ItemActivate = OnDirectoryListViewItemActivateFire,
                    ColumnClick = OnDirectoryListViewColumnClickFire,
                    //ContextMenu = CreateSearchResultContextMenu(),
                    ItemSelectionChanged = OnDirectoryListViewItemSelectionChangedFire
                };

            directoryTreeView.BeforeExpand += DirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect += DirectoryTreeViewOnAfterSelect;

            searchButton.Click += (s, e) => OnSearchRoots();

            toolStripDropDownButton1.ShowDropDownArrow = false;
            //toolStripDropDownButton1.Click += (s, e) => OnLoadData();

            // Enter in pattern Text Box fires Search Button.
            patternTextBox.GotFocus += (s, e) => AcceptButton = searchButton;
            patternTextBox.LostFocus += (s, e) => AcceptButton = null;

            CatalogListViewHelper = new ListViewHelper(catalogResultListView)
                {
                    MultiSelect = false,
                    RetrieveVirtualItem = OnCatalogRetrieveVirtualItemFire,
                    ItemActivate = OnCatalogListViewItemActivateFire,
                };

            directoryPathTextBox.ReadOnly = true; // only for display and manual select copy for now ?

            exitToolStripMenuItem.Click += (s, e) => OnExitMenuItem();
        }

        private ContextMenuStrip CreateSearchResultContextMenu()
        {
            // remove (space for icons on left) for items.
            var menu = new ContextMenuStrip {ShowCheckMargin = false, ShowImageMargin = false};

            var viewTree = new ToolStripMenuItem("View Tree");
            var open = new ToolStripMenuItem("Open");
            var explore = new ToolStripMenuItem("Explore");
            var properties = new ToolStripMenuItem("Properties"); // like explorer -- TODO in treeview to ?
            var selectAll = new ToolStripMenuItem("Select All");
            var parent = new ToolStripMenuItem("Parent"); // for Directory listview parent ? not useful SearchResult

            viewTree.ShortcutKeyDisplayString = "Enter"; // for documentation of ItemActivate which is Enter.
            open.ShortcutKeys = Keys.Control | Keys.Enter;
            explore.ShortcutKeys = Keys.Control | Keys.X;
            properties.ShortcutKeys = Keys.Alt | Keys.Enter;
            selectAll.ShortcutKeys = Keys.Control | Keys.A;
            parent.ShortcutKeys = Keys.Control | Keys.Back;

            viewTree.Click += SearchResultContextMenuViewTreeClick;
            open.Click += SearchResultContextMenuOpenClick;
            explore.Click += (s, e) => OnSearchResultContextMenuExploreClick();

            menu.Items.AddRange(new ToolStripItem[] { viewTree, open, explore, properties, selectAll, parent });

            foreach (ToolStripMenuItem menuItem in menu.Items)
            {
                menuItem.ShowShortcutKeys = true;
                menuItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            }
            return menu;
        }

        private void SearchResultContextMenuOpenClick(object sender, EventArgs e)
        {
            Console.WriteLine("SearchResultContextMenuOpenClick");
        }

        // this depends on SearchResultListViewHelper for the context item.
        // this is ok since this is a left click after the context menu is shown.
        private void SearchResultContextMenuViewTreeClick(object sender, EventArgs e)
        {
            // ? should this be conditional if more than one item is selected dont do anything ?
            // or even disable this menu option if more than one selected ?

            // reuse item activate - which navigates to Directory tab and expands selects item
            // TODO FIx this need to name for Use not its source/dest data.
            SearchResultListViewHelper.AfterActivateIndex = SearchResultListViewHelper.ContextItemIndex;
            OnSearchResultListViewItemActivate();
        }

        private void MyFormActivated(object sender, EventArgs eventArgs)
        {
            patternTextBox.Focus();
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
            get { return patternTextBox.Text; }
            set { patternTextBox.Text = value; }
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

        public bool IncludePathInSearch
        {
            get { return whatToSearchComboBox.SelectedIndex == 0; }
        }

        public void SetSearchResultStatus(int i)
        {
            searchResultsStatus.Text = "SR " + i.ToString();
        }

        public void SetCatalogsLoadedStatus(int i)
        {
            catalogsLoadedStatus.Text = "C " + i.ToString();
        }

        public void SetSearchTextBoxAutoComplete(IEnumerable<string> history)
        {
            patternTextBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            patternTextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            patternTextBox.AutoCompleteCustomSource = history.ToAutoCompleteStringCollection();
        }

        public void AddSearchTextBoxAutoComplete(string pattern)
        {
            var ac = patternTextBox.AutoCompleteCustomSource;
            if (!ac.Contains(pattern))
            {
                ac.Add(pattern);
            }
        }

        public List<string> GetSearchTextBoxAutoComplete()
        {
            var list = new List<string>(20);
            list.AddRange(patternTextBox.AutoCompleteCustomSource.Cast<string>());
            return list;
        }

        public void SelectDirectoryPane()
        {
            mainTabControl.SelectTab(2);
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

        public bool SearchButtonEnable {
            get { return searchButton.Enabled; }
            set
            {
                if (value)
                {   // Consume any events left from disabled button.
                    Application.DoEvents();
                }
                searchButton.Enabled = value;
            }
        }
    }
}
