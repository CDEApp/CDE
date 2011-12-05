using System;
using System.Collections.Generic;
using System.Drawing;
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
        event EventAction OnLoadData;
        event EventAction OnBeforeExpandNode;
        event EventAction OnAfterSelect;
        event EventAction OnSearchRoots;
        event EventAction OnMyFormClosing;

        /// <summary>
        /// Depends on SearchResultListViewItem.
        /// </summary>
        event EventAction OnSearchResultRetrieveVirtualItem;

        TreeNode TreeViewNodes { get;  set; }

        TreeNode ActiveBeforeExpandNode { get; set; }
        TreeNode ActiveAfterSelectNode { get; set; }
        bool CancelExpandEvent { get; set; }
        string Pattern { get; set; }
        bool RegexMode { get; set; }
        bool IncludePathInSearch { get; }

        int SearchResultListViewItemIndex { get; set; }
        ListViewItem SearchResultListViewItem { get; set; }

        void SetDirectoryColumnHeaders(IEnumerable<ColumnConfig> columns);
        void SetSearchColumnHeaders(IEnumerable<ColumnConfig> columns);
        ListView.ColumnHeaderCollection GetDirectoryListViewColumns { get; }
        ListView.ColumnHeaderCollection GetSearchResultListViewColumns { get; }

        void AddDirectoryListViewRow(string[] vals, Color firstColumnForeColor, object tag);
        void AddSearchListViewRow(string[] vals, Color firstColumnForeColor, object tag);
        void SetSearchVirtualList(List<PairDirEntry> pdeList);
        ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag);
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

        private void RegisterClientEvents()
        {
            FormClosing += (s, e) => OnMyFormClosing();

            whatToSearchComboBox.Items.AddRange(new[] {"Include","Exclude"});
            whatToSearchComboBox.SelectedIndex = 0; // default Include


            searchResultListView.View = View.Details; // detail 
            searchResultListView.RetrieveVirtualItem += OnSearchResultListViewOnRetrieveVirtualItem;

            directoryListView.View = View.Details; // detail 

            CancelExpandEvent = false;
            directoryTreeView.BeforeExpand += (s, e) => 
            {
                ActiveBeforeExpandNode = e.Node;
                OnBeforeExpandNode();
                e.Cancel = CancelExpandEvent;
                CancelExpandEvent = false;
            };

            directoryTreeView.AfterSelect += (s, e) => 
            {
                ActiveAfterSelectNode = e.Node;
                OnAfterSelect();
            };

            searchButton.Click += (s, e) => OnSearchRoots();

            toolStripDropDownButton1.ShowDropDownArrow = false;
            toolStripDropDownButton1.Click += (s, e) => OnLoadData();
        }

        private void OnSearchResultListViewOnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            SearchResultListViewItemIndex = e.ItemIndex;
            OnSearchResultRetrieveVirtualItem();
            e.Item = SearchResultListViewItem;
        }

        public event EventAction OnLoadData;
        public event EventAction OnBeforeExpandNode;
        public event EventAction OnAfterSelect;
        public event EventAction OnSearchRoots;
        public int SearchResultListViewItemIndex { get; set; }
        public event EventAction OnSearchResultRetrieveVirtualItem;
        public event EventAction OnMyFormClosing;

        public TreeNode ActiveBeforeExpandNode { get; set; }
        public TreeNode ActiveAfterSelectNode { get; set; }
        public ListViewItem SearchResultListViewItem { get; set; }

        /// <summary>
        /// One shot cancel next expand in BeforExpand, then sets back to false.
        /// </summary>
        public bool CancelExpandEvent { get; set; }

        public string Pattern
        {
            get { return searchComboBox.Text; }
            set { searchComboBox.Text = value; }
        }

        public bool RegexMode
        {
            get { return regexCheckbox.Checked; }
            set { regexCheckbox.Checked = value; }
        }

        /// <summary>
        /// Adds nodes to tree wrapped by BeginUpdate EndUpdate.
        /// </summary>
        public TreeNode TreeViewNodes
        {
            get { return directoryTreeView.Nodes[0]; } // Assumption just one root node.

            set
            {
                if (value != null)
                {
                    directoryTreeView.BeginUpdate();
                    directoryTreeView.Nodes.Add(value);
                    directoryTreeView.SelectedNode = directoryTreeView.Nodes[0];
                    directoryTreeView.Nodes[0].Expand();
                    directoryTreeView.Select();
                    directoryTreeView.EndUpdate();
                }
                // TODO set the value of the detail pane. directoryListView
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

        public void AddDirectoryListViewRow(string[] vals, Color firstColumnForeColor, object tag)
        {
            directoryListView.Items.Add(BuildListViewItem(vals, firstColumnForeColor, tag));
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

        public void AddSearchListViewRow(string[] vals, Color firstColumnForeColor, object tag)
        {
            searchResultListView.Items.Add(BuildListViewItem(vals, firstColumnForeColor, tag));
        }

        public void SetSearchVirtualList(List<PairDirEntry> pdeList)
        {
            searchResultListView.VirtualListSize = pdeList.Count;
            searchResultListView.VirtualMode = true;
            searchResultsStatus.Text = "SR " + pdeList.Count.ToString();
            ForceDrawSearchResultListView();
            ListView.ColumnHeaderCollection a = this.directoryListView.Columns;
        }

        public void ForceDrawSearchResultListView()
        {
            searchResultListView.Invalidate();
        }

        public ListView.ColumnHeaderCollection GetDirectoryListViewColumns
        {
            get { return directoryListView.Columns; }
        }

        public ListView.ColumnHeaderCollection GetSearchResultListViewColumns
        {
            get { return searchResultListView.Columns; }
        }
    }
}
