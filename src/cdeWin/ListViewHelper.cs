using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    // think about making Presenter<> look for all members that implement IPresenterHelper
    // and hookup events to them as well with matching names ?
    // encapsulate ListView in VirtualMode handling

    /// <summary>
    /// Consolidated code for Listview operation in VirtualMode.
    /// Only ListView events required are enabled.
    /// Several property setters add Event handlers as required so dont call them more than once.
    /// </summary>
    public class ListViewHelper<T> where T : class
    {
        private int _listSize;
        private List<T> _list;
        private readonly ListView _listView;
        // very simple caching of LVI's, just remembers the previous index - its a big win for how simple.
        private ListViewItem _cacheListViewItem;
        private int _cacheIndex;

        /// <summary>
        /// Used by virtual mode ListView
        /// </summary>
        public int RetrieveItemIndex { get; set; }
        public ListViewItem RenderItem { get; set; }

        public int AfterActivateIndex { get; set; }
        public int ColumnClickIndex { get; set; }
        public IEnumerable<int> SelectedIndices { get; set; }
        public int SelectedIndicesCount { get; set; }
        public SortOrder ColumnSortOrder { get; set; }
        public int SortColumn { get; set; }
        public Comparison<T> ColumnSortCompare { get; set; }

        /// <summary>
        /// The item under the mouse when context menu launched, ie right mouse button up.
        /// Need to be reset after use... If set to -1 its not a right click context value.
        /// </summary>
        public int ContextItemIndex { get; set; }

        public ListViewHelper(ListView listView)
        {
            _listView = listView;
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
            _listView.Activation = ItemActivation.Standard;
            _listView.VirtualMode = true;
            InitSort();
        }

        public void InitSort()
        {
            SortColumn = 0;
            ColumnSortOrder = SortOrder.Ascending;
        }

        /// <summary>
        /// Adds CacheVirtualItems, RetrieveVirtualItem handler which sets RetrieveItemIndex before EventAction.
        /// </summary>
        public EventAction RetrieveVirtualItem
        {
            get { return _retrieveVirtualItem; } 
            set
            {
                _retrieveVirtualItem = value;
                _listView.CacheVirtualItems += MyCacheVirtualItems;
                _listView.RetrieveVirtualItem += MyRetrieveVirtualItem;
            }
        }
        private EventAction _retrieveVirtualItem;

        /// <summary>
        /// Adds ColumnClick handler which sets ColumnClickIndex before EventAction..
        /// </summary>
        public EventAction ColumnClick
        {
            get { return _columnClick;  }
            set
            {
                _columnClick = value;
                _listView.ColumnClick += MyColumnClick;
            }
        }
        private EventAction _columnClick;

        /// <summary>
        /// Adds ItemActivate handler which sets AfterActivateIndex before EventAction..
        /// </summary>
        public EventAction ItemActivate
        {
            get { return _itemActivate; }
            set
            {
                _itemActivate = value;
                _listView.ItemActivate += MyItemActivate;
            }
        }
        private EventAction _itemActivate;

        /// <summary>
        /// Adds MouseUp handler which sets ContextItemIndex on right mouse button up.
        /// </summary>
        public ContextMenuStrip ContextMenu
        {
            get { return _contextMenu; }
            set
            {
                _contextMenu = value;
                _listView.MouseUp += MyMouseUp;
                _listView.ContextMenuStrip = _contextMenu;
            }
        }
        private ContextMenuStrip _contextMenu;

        /// <summary>
        /// Adds SelectedIndexChanged, VirtualItemsSelectionRangeChanged handlers.
        /// </summary>
        public EventAction ItemSelectionChanged
        {
            get { return _itemSelectionChanged; }
            set
            {
                _itemSelectionChanged = value;
                _listView.SelectedIndexChanged += MySelectedIndexChanged;
                _listView.VirtualItemsSelectionRangeChanged += MyVirtualItemsSelectionRangeChanged;
            }
        }
        private EventAction _itemSelectionChanged;

        public bool MultiSelect
        {
            get { return _listView.MultiSelect; }
            set { _listView.MultiSelect = value; }
        }

        /// <summary>
        /// Simplest invalidation of ListViewItem cache.
        /// Now a single line view will not cache for ever.
        /// </summary>
        private void MyCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            _cacheIndex = -1;
        }

        /// <summary>
        /// Handles very simple caching of ListViewItem just the current Index is remembered.
        /// </summary>
        private void MyRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var itemIndex = e.ItemIndex;
            if (_cacheIndex != itemIndex)
            {
                RetrieveItemIndex = itemIndex;
                _retrieveVirtualItem();
                _cacheIndex = itemIndex;
                _cacheListViewItem = RenderItem;
            }
            e.Item = _cacheListViewItem;
        }

        private void MyColumnClick(object sender, ColumnClickEventArgs e)
        {
            ColumnClickIndex = e.Column;
            _columnClick();
        }

        private void MyItemActivate(object sender, EventArgs e)
        {
            // Only activate if single item selected for now.
            if (_listView.SelectedIndices.Count == 1)
            {
                AfterActivateIndex = _listView.SelectedIndices[0];
                _itemActivate();
            }
        }

        private void MyMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = _listView.HitTest(e.Location).Item;
                if (item != null)
                {
                    ContextItemIndex = item.Index;
                }
            }
        }

        private void MySelectedIndexChanged(object sender, EventArgs e)
        {
            DirectoryListViewItemSelectionChanged();
        }

        private void MyVirtualItemsSelectionRangeChanged(object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            DirectoryListViewItemSelectionChanged();
        }

        private void DirectoryListViewItemSelectionChanged()
        {
            SelectedIndicesCount = _listView.SelectedIndices.Count; // todo can i lose this ?
            SelectedIndices = _listView.SelectedIndices.OfType<int>();
            if (SelectedIndicesCount > 0)
            {
                _itemSelectionChanged();
            }
        }

        public IEnumerable<ColumnConfig> ColumnConfigs()
        {
            return _listView.Columns.ColumnConfigs();
        }

        public void SetColumnConfigs(IEnumerable<ColumnConfig> columns)
        {
            _listView.SetColumnHeaders(columns);
        }

        public void ForceDraw()
        {
            _listView.Invalidate();
        }

        public void SelectItem(int index)
        {
            if (_listView.Items.Count <= index)
            {
                return;
            }

            var listItem = _listView.Items[index];
            listItem.Selected = true;
            listItem.Focused = true;
            listItem.EnsureVisible();
            _listView.Select();
        }

        public void DeselectItems()
        {
            for (var i = 0; i < _listView.Items.Count; i++)
            {
                var listItem = _listView.Items[i];
                if (listItem.Selected)
                {
                    listItem.Selected = false;
                }
            }
        }

        public void SelectAllItems()
        {
            for (var i = 0; i < _listView.Items.Count; i++)
            {
                var listItem = _listView.Items[i];
                if (!listItem.Selected)
                {
                    listItem.Selected = true;
                }
            }
        }

        public int SetList(List<T> list)
        {
            if (_columnClick != null && ColumnSortCompare == null)
            {
                throw new Exception("ListViewHelper with ColumnClick requires value for ColumnSortCompare.");
            }
            _list = list;
            _listSize = _list == null ? 0 : _list.Count();
            _listView.VirtualListSize = _listSize;
            SortList();
            return _listSize;
        }

        public void ListViewColumnClick()
        {
            var newSortColumn = ColumnClickIndex;
            if (SortColumn == newSortColumn)
            {
                ColumnSortOrder = (ColumnSortOrder == SortOrder.Ascending) 
                            ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                SortColumn = newSortColumn;
                ColumnSortOrder = SortOrder.Ascending;
            }
            SortList();
        }

        public void SortList()
        {
            if (_list == null)
            {
                return;
            }
            _list.Sort(ColumnSortCompare);
            ForceDraw();
        }

        public void ActionOnContextItem(Action<T> action)
        {
            var contextItem = GetContextItem();
            if (contextItem != null)
            {
                action(contextItem);
            }
        }

        private T GetContextItem()
        {
            if (!(ContextItemIndex >= 0) || _list == null)
            {
                return null;
            }
            var item = _list[ContextItemIndex];
            ContextItemIndex = -1;
            return item;
        }

        public void ActionOnActivateItem(Action<T> action)
        {
            var activateItem = GetActivateItem();
            if (activateItem != null)
            {
                action(activateItem);
            }
        }

        private T GetActivateItem()
        {
            if (!(AfterActivateIndex >= 0) || _list == null)
            {
                return null;
            }
            var item = _list[AfterActivateIndex];
            AfterActivateIndex = -1;
            return item;
        }
    }
}