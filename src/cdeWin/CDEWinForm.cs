using System;
using System.Collections.Generic;
using System.Drawing;
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
        event EventAction OnSearchRoots;
        event EventAction OnSearchResultRetrieveVirtualItem;
        event EventAction OnDirectoryRetrieveVirtualItem;
        event EventAction OnMyFormClosing;
        event EventAction OnCatalogListViewItemActivate;
        event EventAction OnDirectoryListViewItemActivate;
        event EventAction OnSearchResultListViewItemActivate;
        event EventAction OnDirectoryListViewItemSelectionChanged;
        event EventAction OnSearchResultListViewColumnClick;
        event EventAction OnDirectoryListViewColumnClick;
        event EventAction OnExitMenuItem;

        TreeNode DirectoryTreeViewNodes { get;  set; }

        TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }
        bool DirectoryTreeViewCancelExpandEvent { get; set; }

        string Pattern { get; set; }
        bool RegexMode { get; set; }
        bool IncludePathInSearch { get; }

        int SearchResultListViewItemIndex { get; set; }
        int DirectoryListViewItemIndex { get; set; }
        ListViewItem SearchResultListViewItem { get; set; }
        ListViewItem DirectoryListViewItem { get; set; }
        IEnumerable<int> DirectorySelectedIndices { get; set; }
        int DirectorySelectedIndicesCount { get; set; }
        
        void SetDirectoryColumnHeaders(IEnumerable<ColumnConfig> columns);
        void SetSearchColumnHeaders(IEnumerable<ColumnConfig> columns);
        IEnumerable<ColumnConfig> GetDirectoryListViewColumns { get; }
        IEnumerable<ColumnConfig> GetSearchResultListViewColumns { get; }
        IEnumerable<ColumnConfig> GetCatalogListViewColumns { get; }

        void AddCatalogListViewRow(string[] vals, Color firstColumnForeColor, object tag);
        void SetSearchResultVirtualListSize(int listSize);
        void SetDirectoryVirtualListSize(int listSize);
        ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag);

        void SetSearchTextBoxAutoComplete(IEnumerable<string> history);
        void AddSearchTextBoxAutoComplete(string pattern);
        List<string> GetSearchTextBoxAutoComplete();
        RootEntry ActiveCatalogAfterSelectRootEntry { get; }
        int ActiveDirectoryListViewIndexAfterActivate { get; }
        int ActiveSearchResultIndexAfterActivate { get; }
        void SelectDirectoryPane();
        float DirectoryPanelSplitterRatio { get; set; }
        string SetDirectoryPathTextbox { set; }
        TreeNode SetDirectoryTreeViewSelectedNode { set; }
        void DirectoryListViewDeselectItems();
        void SelectDirectoryListViewItem(int index);
        void SetCatalogsLoadedStatus(int i);
        int SearchResultListViewColumnIndex { get; }
        int DirectoryListViewColumnIndex { get; }
        void ForceDrawSearchResultListView();
        void ForceDrawDirectoryListView();
        bool SearchButtonEnable { get; set; }
    }

    public partial class CDEWinForm : Form, ICDEWinForm
    {
        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearchRoots;
        public event EventAction OnSearchResultRetrieveVirtualItem;
        public event EventAction OnDirectoryRetrieveVirtualItem;
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogListViewItemActivate;
        public event EventAction OnDirectoryListViewItemActivate;
        public event EventAction OnSearchResultListViewItemActivate;
        public event EventAction OnDirectoryListViewItemSelectionChanged;
        public event EventAction OnSearchResultListViewColumnClick;
        public event EventAction OnDirectoryListViewColumnClick;
        public event EventAction OnExitMenuItem;
        
        public int SearchResultListViewItemIndex { get; set; }
        public int DirectoryListViewItemIndex { get; set; }
        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }
        public CommonEntry ActiveDirectoryAfterSelectNode { get; set; }
        public ListViewItem SearchResultListViewItem { get; set; }
        public ListViewItem DirectoryListViewItem { get; set; }
        public IEnumerable<int> DirectorySelectedIndices { get; set; }
        public int DirectorySelectedIndicesCount { get; set; }
        public int SearchResultListViewColumnIndex { get; set; }
        public int DirectoryListViewColumnIndex { get; set; }

        /// <summary>
        /// One shot cancel next expand in BeforExpand, then sets back to false.
        /// </summary>
        public bool DirectoryTreeViewCancelExpandEvent { get; set; }

        public CDEWinForm()
        {
            InitializeComponent();
            AutoWaitCursor.Cursor = Cursors.WaitCursor;
            AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
            AutoWaitCursor.MainWindowHandle = Handle;
            AutoWaitCursor.Start();
            RegisterClientEvents();
        }

        // sets some configuration stuff to... ? hmmm split or not good idea ? 

        private void RegisterClientEvents()
        {
            FormClosing += (s, e) => OnMyFormClosing();
            Activated += OnMyFormActivated;

            whatToSearchComboBox.Items.AddRange(new[] {"Include","Exclude"});
            whatToSearchComboBox.SelectedIndex = 0; // default Include

            searchResultListView.View = View.Details;
            searchResultListView.FullRowSelect = true;
            searchResultListView.RetrieveVirtualItem += OnSearchResultListViewOnRetrieveVirtualItem;
            searchResultListView.ItemActivate += OnSearchresultListViewOnItemActivate;
            searchResultListView.ColumnClick += OnSearchResultListViewOnColumnClick;

            directoryListView.View = View.Details;
            directoryListView.FullRowSelect = true;
            directoryListView.RetrieveVirtualItem += OnDirectoryListViewOnRetrieveVirtualItem;
            directoryListView.ItemActivate += OnDirectoryListViewOnItemActivate;
            directoryListView.ColumnClick += OnDirectoryListViewOnColumnClick;

            // require both events for behaviour I want of DirectoryPathTextBox
            directoryListView.SelectedIndexChanged += OnDirectoryListViewOnItemSelectionChanged;
            directoryListView.VirtualItemsSelectionRangeChanged += OnDirectoryListViewOnItemSelectionChanged;

            DirectoryTreeViewCancelExpandEvent = false;
            directoryTreeView.BeforeExpand += OnDirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect += OnDirectoryTreeViewOnAfterSelect;

            searchButton.Click += (s, e) => OnSearchRoots();

            toolStripDropDownButton1.ShowDropDownArrow = false;
            //toolStripDropDownButton1.Click += (s, e) => OnLoadData();

            // Enter in Search Text Box fires Search Button.
            patternTextBox.GotFocus += (s, e) => AcceptButton = searchButton;
            patternTextBox.LostFocus += (s, e) => AcceptButton = null;

            catalogResultListView.MultiSelect = false;
            catalogResultListView.View = View.Details;
            catalogResultListView.FullRowSelect = true;
            catalogResultListView.Activation = ItemActivation.Standard;
            catalogResultListView.ItemActivate += OnCatalogListViewOnItemActivate;

            directoryPathTextBox.ReadOnly = true; // only for display and manual select copy for now ?

            exitToolStripMenuItem.Click += (s, e) => OnExitMenuItem();
        }

        private void OnDirectoryListViewOnColumnClick(object s, ColumnClickEventArgs e)
        {
            DirectoryListViewColumnIndex = e.Column;
            OnDirectoryListViewColumnClick();
        }

        private void OnSearchResultListViewOnColumnClick(object s, ColumnClickEventArgs e)
        {
            SearchResultListViewColumnIndex = e.Column;
            OnSearchResultListViewColumnClick();
        }

        private void OnMyFormActivated(object sender, EventArgs eventArgs)
        {
            patternTextBox.Focus();
        }

        private void OnDirectoryTreeViewOnAfterSelect(object s, TreeViewEventArgs e)
        {
            DirectoryTreeViewActiveAfterSelectNode = e.Node;
            OnDirectoryTreeViewAfterSelect();
        }

        private void OnDirectoryTreeViewOnBeforeExpand(object s, TreeViewCancelEventArgs e)
        {
            DirectoryTreeViewActiveBeforeExpandNode = e.Node;
            OnDirectoryTreeViewBeforeExpandNode();
            e.Cancel = DirectoryTreeViewCancelExpandEvent;
            DirectoryTreeViewCancelExpandEvent = false;
        }

        private void OnDirectoryListViewOnItemSelectionChanged(object s, EventArgs e)
        {
            DirectorySelectedIndices = directoryListView.SelectedIndices.OfType<int>();
            DirectorySelectedIndicesCount = directoryListView.SelectedIndices.Count;
            if (directoryListView.SelectedIndices.Count > 0)
            {
                OnDirectoryListViewItemSelectionChanged();
            }
        }

        private void OnCatalogListViewOnItemActivate(object sender, EventArgs e)
        {
            // Catalog List View has multi select off.
            ActiveCatalogAfterSelectRootEntry = (RootEntry)catalogResultListView.SelectedItems[0].Tag;
            OnCatalogListViewItemActivate();
        }

        private void OnDirectoryListViewOnItemActivate(object sender, EventArgs e)
        {
            // Only activate if single item selected for now.
            if (directoryListView.SelectedIndices.Count == 1)
            {
                ActiveDirectoryListViewIndexAfterActivate = directoryListView.SelectedIndices[0];
                OnDirectoryListViewItemActivate();
            }
        }

        private void OnSearchresultListViewOnItemActivate(object sender, EventArgs e)
        {
            // Only activate if single item selected for now.
            if (searchResultListView.SelectedIndices.Count == 1)
            {
                ActiveSearchResultIndexAfterActivate = searchResultListView.SelectedIndices[0];
                OnSearchResultListViewItemActivate();
            }
        }

        public RootEntry ActiveCatalogAfterSelectRootEntry { get; private set; }

        public int ActiveDirectoryListViewIndexAfterActivate { get; private set; }

        public int ActiveSearchResultIndexAfterActivate { get; private set; }

        private void OnSearchResultListViewOnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            SearchResultListViewItemIndex = e.ItemIndex;
            OnSearchResultRetrieveVirtualItem();
            e.Item = SearchResultListViewItem;
        }

        private void OnDirectoryListViewOnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            DirectoryListViewItemIndex = e.ItemIndex;
            OnDirectoryRetrieveVirtualItem();
            e.Item = DirectoryListViewItem;
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

        /// <summary>
        /// Clear list view and set column headers.
        /// </summary>
        public void SetDirectoryColumnHeaders(IEnumerable<ColumnConfig> columns)
        {
            SetColumnHeaders(directoryListView, columns);
        }

        public void SetColumnHeaders(ListView lv, IEnumerable<ColumnConfig> columns)
        {
            lv.Clear();
            if (columns != null)
            {
                foreach (var col in columns)
                {
                    lv.Columns.Add(col.Name, col.Width, col.Alignment);
                }
            }
        }

        public ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag)
        {
            var lvItem = new ListViewItem(vals[0]) {UseItemStyleForSubItems = false};
            lvItem.SubItems[0].ForeColor = firstColumnForeColor;
            lvItem.Tag = tag;
            for (var i = 1; i < vals.Length; ++i)
            {
                lvItem.SubItems.Add(vals[i]);
            }
            return lvItem;
        }

        public bool IncludePathInSearch
        {
            get { return whatToSearchComboBox.SelectedIndex == 0; }
        }

        public void SetSearchColumnHeaders(IEnumerable<ColumnConfig> columns)
        {
            SetColumnHeaders(searchResultListView, columns);
        }

        public void AddCatalogListViewRow(string[] vals, Color firstColumnForeColor, object tag)
        {
            catalogResultListView.Items.Add(BuildListViewItem(vals, firstColumnForeColor, tag));
        }

        public void SetSearchResultVirtualListSize(int listSize)
        {
            searchResultListView.VirtualListSize = listSize;
            searchResultListView.VirtualMode = true;
            SetSearchResultStatus(listSize);
            ForceDrawSearchResultListView();
        }

        public void SetSearchResultStatus(int i)
        {
            searchResultsStatus.Text = "SR " + i.ToString();
        }

        public void SetCatalogsLoadedStatus(int i)
        {
            catalogsLoadedStatus.Text = "C " + i.ToString();
        }

        public void ForceDrawSearchResultListView()
        {
            searchResultListView.Invalidate();
        }

        public void SetDirectoryVirtualListSize(int listSize)
        {
            directoryListView.VirtualListSize = listSize;
            directoryListView.VirtualMode = true;
            ForceDrawDirectoryListView();
        }

        public void ForceDrawDirectoryListView()
        {
            directoryListView.Invalidate();
        }

        public IEnumerable<ColumnConfig> GetDirectoryListViewColumns
        {
            get { return ColumnConfigs(directoryListView.Columns); }
        }

        public IEnumerable<ColumnConfig> GetSearchResultListViewColumns
        {
            get { return ColumnConfigs(searchResultListView.Columns); }
        }

        public IEnumerable<ColumnConfig> GetCatalogListViewColumns
        {
            get { return ColumnConfigs(catalogResultListView.Columns); }
        }

        private IEnumerable<ColumnConfig> ColumnConfigs(ListView.ColumnHeaderCollection chc)
        {
            var columns = chc.OfType<ColumnHeader>();
            return columns.Select(columnHeader =>
                                  new ColumnConfig
                                      {
                                          Name = columnHeader.Text,
                                          Width = columnHeader.Width
                                      });
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

        public void SetCatalogColumnHeaders(IEnumerable<ColumnConfig> columns)
        {
            SetColumnHeaders(catalogResultListView, columns);
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

        public void DirectoryListViewDeselectItems()
        {
            for (var i = 0; i < directoryListView.Items.Count; i++)
            {
                var listItem = directoryListView.Items[i];
                if (listItem.Selected)
                {
                    listItem.Selected = false;
                }
            }
        }

        public void SelectDirectoryListViewItem(int index)
        {
            var listItem = directoryListView.Items[index];
            listItem.Selected = true;
            listItem.Focused = true;
            listItem.EnsureVisible();
            directoryListView.Select();
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
