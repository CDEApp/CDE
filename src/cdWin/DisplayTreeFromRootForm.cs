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

    public interface IDisplayTreeFromRootForm : IView
    {
        event EventAction OnDirectoryTreeViewBeforeExpandNode;
        event EventAction OnDirectoryTreeViewAfterSelect;
        event EventAction OnSearchRoots;
        event EventAction OnMyFormClosing;
        event EventAction OnCatalogListViewItemActivate;

        /// <summary>
        /// Depends on SearchResultListViewItem.
        /// </summary>
        event EventAction OnSearchResultRetrieveVirtualItem;
        event EventAction OnDirectoryRetrieveVirtualItem;

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

        void SetDirectoryColumnHeaders(IEnumerable<ColumnConfig> columns);
        void SetSearchColumnHeaders(IEnumerable<ColumnConfig> columns);
        ListView.ColumnHeaderCollection GetDirectoryListViewColumns { get; }
        ListView.ColumnHeaderCollection GetSearchResultListViewColumns { get; }
        ListView.ColumnHeaderCollection GetCatalogListViewColumns { get; }

        void AddCatalogListViewRow(string[] vals, Color firstColumnForeColor, object tag);
        void SetSearchResultVirtualList(List<PairDirEntry> pdeList);
        void SetDirectoryVirtualList(CommonEntry parentCommonEntry);
        ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag);

        void SetSearchTextBoxAutoComplete(IEnumerable<string> history);
        void AddSearchTextBoxAutoComplete(string pattern);
        List<string> GetSearchTextBoxAutoComplete();
        RootEntry ActiveCatalogAfterSelectRootEntry { get; }
    }

    public partial class DisplayTreeFromRootFormForm : Form, IDisplayTreeFromRootForm
    {
        public DisplayTreeFromRootFormForm()
        {
            InitializeComponent();
            RegisterClientEvents();
            //Console.WriteLine("Width " + Size.Width);
            //Console.WriteLine("Height " + Size.Height);
        }

        // sets some configuration stuff to... ? hmmm split or not good idea ? 
        private void RegisterClientEvents()
        {
            FormClosing += (s, e) => OnMyFormClosing();

            whatToSearchComboBox.Items.AddRange(new[] {"Include","Exclude"});
            whatToSearchComboBox.SelectedIndex = 0; // default Include

            searchResultListView.View = View.Details;
            searchResultListView.FullRowSelect = true;
            searchResultListView.RetrieveVirtualItem += OnSearchResultListViewOnRetrieveVirtualItem;

            directoryListView.View = View.Details;
            directoryListView.FullRowSelect = true;
            directoryListView.RetrieveVirtualItem += OnDirectoryListViewOnRetrieveVirtualItem;

            DirectoryTreeViewCancelExpandEvent = false;
            directoryTreeView.BeforeExpand += (s, e) => 
            {
                DirectoryTreeViewActiveBeforeExpandNode = e.Node;
                OnDirectoryTreeViewBeforeExpandNode();
                e.Cancel = DirectoryTreeViewCancelExpandEvent;
                DirectoryTreeViewCancelExpandEvent = false;
            };

            directoryTreeView.AfterSelect += (s, e) => 
            {
                DirectoryTreeViewActiveAfterSelectNode = e.Node;
                OnDirectoryTreeViewAfterSelect();
            };

            searchButton.Click += (s, e) => OnSearchRoots();

            toolStripDropDownButton1.ShowDropDownArrow = false;
            //toolStripDropDownButton1.Click += (s, e) => OnLoadData();

            // Enter in Search Text Box fires Search Button.
            searchTextBox.GotFocus += (s, e) => AcceptButton = searchButton;
            searchTextBox.LostFocus += (s, e) => AcceptButton = null;

            AutoWaitCursor.Cursor = Cursors.WaitCursor;
            AutoWaitCursor.Delay = new TimeSpan(0,0,0,0,25);
            AutoWaitCursor.MainWindowHandle = Handle;
            AutoWaitCursor.Start();

            catalogResultListView.MultiSelect = false;
            catalogResultListView.View = View.Details;
            catalogResultListView.FullRowSelect = true;
            catalogResultListView.Activation = ItemActivation.Standard;
            catalogResultListView.ItemActivate += OnCatalogResultListViewOnItemActivate;
        }

        private void OnCatalogResultListViewOnItemActivate(object sender, EventArgs e)
        {
            // Catalog List View has multi select off.
            ActiveCatalogAfterSelectRootEntry = catalogResultListView.SelectedItems[0].Tag as RootEntry;
            OnCatalogListViewItemActivate();
        }

        public RootEntry ActiveCatalogAfterSelectRootEntry { get; private set; }


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

        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearchRoots;
        public int SearchResultListViewItemIndex { get; set; }
        public int DirectoryListViewItemIndex { get; set; }
        public event EventAction OnSearchResultRetrieveVirtualItem;
        public event EventAction OnDirectoryRetrieveVirtualItem;
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogListViewItemActivate;

        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }
        public CommonEntry ActiveDirectoryAfterSelectNode { get; set; }
        public ListViewItem SearchResultListViewItem { get; set; }
        public ListViewItem DirectoryListViewItem { get; set; }

        /// <summary>
        /// One shot cancel next expand in BeforExpand, then sets back to false.
        /// </summary>
        public bool DirectoryTreeViewCancelExpandEvent { get; set; }

        public string Pattern
        {
            get { return searchTextBox.Text; }
            set { searchTextBox.Text = value; }
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
            get { return directoryTreeView.Nodes[0]; } // Assumption just one root node.

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

        public void SetSearchResultVirtualList(List<PairDirEntry> pdeList)
        {
            searchResultListView.VirtualListSize = pdeList.Count;
            searchResultListView.VirtualMode = true;
            searchResultsStatus.Text = "SR " + pdeList.Count.ToString();
            ForceDrawSearchResultListView();
        }

        public void ForceDrawSearchResultListView()
        {
            searchResultListView.Invalidate();
        }

        public void SetDirectoryVirtualList(CommonEntry parentCommonEntry)
        {
            directoryListView.VirtualListSize = parentCommonEntry.Children.Count;
            directoryListView.VirtualMode = true;
            ForceDrawDirectoryListView();
        }

        public void ForceDrawDirectoryListView()
        {
            directoryListView.Invalidate();
        }

        public ListView.ColumnHeaderCollection GetDirectoryListViewColumns
        {
            get { return directoryListView.Columns; }
        }

        public ListView.ColumnHeaderCollection GetSearchResultListViewColumns
        {
            get { return searchResultListView.Columns; }
        }

        public ListView.ColumnHeaderCollection GetCatalogListViewColumns
        {
            get { return catalogResultListView.Columns; }
        }

        public void SetSearchTextBoxAutoComplete(IEnumerable<string> history)
        {
            searchTextBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            searchTextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            searchTextBox.AutoCompleteCustomSource = history.ToAutoCompleteStringCollection();
        }

        public void AddSearchTextBoxAutoComplete(string pattern)
        {
            var ac = searchTextBox.AutoCompleteCustomSource;
            if (!ac.Contains(pattern))
            {
                ac.Add(pattern);
            }
        }

        public List<string> GetSearchTextBoxAutoComplete()
        {
            var list = new List<string>(20);
            list.AddRange(searchTextBox.AutoCompleteCustomSource.Cast<string>());
            return list;
        }

        public void SetCatalogColumnHeaders(IEnumerable<ColumnConfig> columns)
        {
            SetColumnHeaders(catalogResultListView, columns);
        }
    }

    public static class EnumerableExtensionsEx
    {
        public static AutoCompleteStringCollection ToAutoCompleteStringCollection(this IEnumerable<string> enumerable)
        {
            if (enumerable == null) throw new ArgumentNullException("enumerable");
            var autoComplete = new AutoCompleteStringCollection();
            foreach (var item in enumerable)
            {
                autoComplete.Add(item);
            }
            return autoComplete;
        }
    }
}
