using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    // think about making Presenter<> look for all members that implement IPresenterHelper
    // and hookup events to them as well with matching names ?
    // encapsulate ListView in VirtualMode handling

    public interface IListViewHelper<T> : IDisposable where T : class
    {
        /// <summary>
        /// Used by virtual mode ListView
        /// </summary>
        int RetrieveItemIndex { get; set; }

        ListViewItem RenderItem { get; set; }
        int AfterActivateIndex { get; set; }
        int ColumnClickIndex { get; set; }
        IEnumerable<int> SelectedIndices { get; set; }
        int SelectedIndicesCount { get; set; }
        SortOrder ColumnSortOrder { get; set; }
        int SortColumn { get; set; }
        Comparison<T> ColumnSortCompare { get; set; }

        /// <summary>
        /// Adds CacheVirtualItems, RetrieveVirtualItem handler which sets RetrieveItemIndex before EventAction.
        /// </summary>
        EventAction RetrieveVirtualItem { get; set; }

        /// <summary>
        /// Adds ColumnClick handler which sets ColumnClickIndex before EventAction..
        /// </summary>
        EventAction ColumnClick { get; set; }

        /// <summary>
        /// Adds ItemActivate handler which sets AfterActivateIndex before EventAction..
        /// </summary>
        EventAction ItemActivate { get; set; }

        ContextMenuStrip ContextMenu { get; set; }

        /// <summary>
        /// Adds SelectedIndexChanged, VirtualItemsSelectionRangeChanged handlers.
        /// </summary>
        EventAction ItemSelectionChanged { get; set; }

        bool MultiSelect { get; set; }

        void InitSort();
        IEnumerable<ColumnConfig> ColumnConfigs();
        void SetColumnConfigs(IEnumerable<ColumnConfig> columns);
        void ForceDraw();
        void SelectItem(int index);
        void DeselectAllItems();
        void SelectAllItems();
        int SetList(List<T> list);
        void ListViewColumnClick();
        void SortList();
        void ActionOnSelectedItems(Action<IEnumerable<T>> action);
        void ActionOnSelectedItem(Action<T> action);
        void ActionOnActivateItem(Action<T> action);
        void SearchListContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e);
    }

    /// <summary>
    /// Consolidated code for ListView operation in VirtualMode.
    /// Only ListView events required are enabled.
    /// Several property setters add Event handlers as required so don't call them more than once.
    /// </summary>
    public class ListViewHelper<T> : IListViewHelper<T> where T : class
    {
        private bool isDisposed;
        private int _listSize;
        private List<T> _list;

        private readonly DoubleBufferListView _listView;

        // very simple caching of ListViewItem's, just remembers the previous index - its a big win for how simple.
        private ListViewItem _cacheListViewItem;
        private int _cacheIndex;

        /// <summary>
        /// Used by virtual mode ListView
        /// </summary>
        public int RetrieveItemIndex { get; set; }

        /// <summary>
        /// Used by virtual mode ListView
        /// </summary>
        public ListViewItem RenderItem { get; set; }

        public int AfterActivateIndex { get; set; }
        public int ColumnClickIndex { get; set; }
        public IEnumerable<int> SelectedIndices { get; set; }
        public int SelectedIndicesCount { get; set; }
        public SortOrder ColumnSortOrder { get; set; }
        public int SortColumn { get; set; }
        public Comparison<T> ColumnSortCompare { get; set; }

        public ListViewHelper(DoubleBufferListView listView)
        {
            _listView = listView;
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
            _listView.Activation = ItemActivation.Standard;
            _listView.VirtualMode = true;
            _listView.GridLines = true;
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
            get => _retrieveVirtualItem;
            set
            {
                // not adding retrieve virtual item events here as _list may not be set
                // was getting some odd errors earlier, this may address the null 
                // ListViewItem we got outside of visual studio in release builds.
                _retrieveVirtualItem = value;
                if (_retrieveVirtualItem != null)
                {
                    // TODO AUDIT - this should probably add if not null, and remove if null?
                    _listView.CacheVirtualItems += MyCacheVirtualItems;
                    _listView.RetrieveVirtualItem += MyRetrieveVirtualItem;
                }
            }
        }

        private EventAction _retrieveVirtualItem;

        /// <summary>
        /// Adds ColumnClick handler which sets ColumnClickIndex before EventAction..
        /// </summary>
        public EventAction ColumnClick
        {
            get => _columnClick;
            set
            {
                _columnClick = value;
                if (_columnClick != null)
                {
                    // TODO AUDIT - this should probably add if not null, and remove if null?
                    _listView.ColumnClick += MyColumnClick;
                }
            }
        }

        private EventAction _columnClick;

        /// <summary>
        /// Adds ItemActivate handler which sets AfterActivateIndex before EventAction..
        /// </summary>
        public EventAction ItemActivate
        {
            get => _itemActivate;
            set
            {
                _itemActivate = value;
                if (_itemActivate != null)
                {
                    // TODO AUDIT - this should probably add if not null, and remove if null?
                    _listView.ItemActivate += MyItemActivate;
                }
            }
        }

        private EventAction _itemActivate;

        public ContextMenuStrip ContextMenu
        {
            get => _contextMenu;
            set
            {
                _contextMenu = value;
                if (_contextMenu != null)
                {
                    // TODO AUDIT - this should probably remove context menu if null?
                    _listView.ContextMenuStrip = _contextMenu;
                }
            }
        }

        private ContextMenuStrip _contextMenu;

        /// <summary>
        /// Adds SelectedIndexChanged, VirtualItemsSelectionRangeChanged handlers.
        /// </summary>
        public EventAction ItemSelectionChanged
        {
            get => _itemSelectionChanged;
            set
            {
                _itemSelectionChanged = value;
                if (_itemSelectionChanged != null)
                {
                    // TODO AUDIT - this should probably add if not null, and remove if null?
                    _listView.SelectedIndexChanged += MySelectedIndexChanged;
                    _listView.VirtualItemsSelectionRangeChanged += MyVirtualItemsSelectionRangeChanged;
                }
            }
        }

        private EventAction _itemSelectionChanged;

        public bool MultiSelect
        {
            get => _listView.MultiSelect;
            set => _listView.MultiSelect = value;
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
            // _cacheListViewItem null check is required...
            // its possible its null when _cacheIndex == itemIndex.
            if (_cacheIndex != itemIndex || _cacheListViewItem == null)
            {
                RetrieveItemIndex = itemIndex;
                RenderItem = null;
                _retrieveVirtualItem();
                _cacheIndex = itemIndex;
                _cacheListViewItem =
                    RenderItem ?? throw new Exception("ListViewItem not retrieved... for " + typeof(T));
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

        private void MySelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSelectionChanged();
        }

        private void MyVirtualItemsSelectionRangeChanged(object sender,
            ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            ListViewItemSelectionChanged();
        }

        private void ListViewItemSelectionChanged()
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

        public void DeselectAllItems()
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

        // Cannot use SelectItem() in a loop as it does Focus on each item.
        public void SelectItems(IEnumerable<int> itemIndices)
        {
            var minIndex = int.MaxValue;
            foreach (var i in itemIndices)
            {
                minIndex = Math.Min(minIndex, i);
                var listItem = _listView.Items[i];
                listItem.Selected = true;
            }

            if (minIndex != int.MaxValue)
            {
                var minimumItem = _listView.Items[minIndex];
                minimumItem.Focused = true;
                minimumItem.EnsureVisible();
            }

            _listView.Select();
        }

        public void SelectItems(IEnumerable<T> itemList)
        {
            var newIndices = itemList.Select(item => _list.FindIndex(sortedItem => item == sortedItem));
            SelectItems(newIndices);
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
                throw new InvalidOperationException(
                    "ListViewHelper with ColumnClick requires value for ColumnSortCompare.");
            }

            _list = list;
            _listSize = _list?.Count() ?? 0;
            _listView.VirtualListSize = _listSize;
            ForceDraw();
            return _listSize;
        }

        public void ListViewColumnClick()
        {
            var newSortColumn = ColumnClickIndex;
            if (SortColumn == newSortColumn)
            {
                ColumnSortOrder = ColumnSortOrder == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
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
            SetColumnSortArrow();
            if (_list != null)
            {
                var selectedItems = GetSelectedItems().ToList(); // ToList() need results before deselect
                DeselectAllItems();
                _list.Sort(ColumnSortCompare);
                SelectItems(selectedItems);
                ForceDraw();
            }
        }

        private void SetColumnSortArrow()
        {
            _listView.SetSortIcon(SortColumn,
                (ColumnSortOrder == SortOrder.Ascending)
                    ? SortOrder.Descending
                    : SortOrder.Ascending); // column state is inverted some how ?
        }

        public void ActionOnSelectedItems(Action<IEnumerable<T>> action)
        {
            var selectedItems = GetSelectedItems();
            if (selectedItems != null)
            {
                action(selectedItems);
            }
        }

        private IEnumerable<T> GetSelectedItems()
        {
            return _listView.SelectedIndices.Cast<int>().Select(i => _list[i]);
        }

        public void ActionOnSelectedItem(Action<T> action)
        {
            var selectedItem = GetSelectedItem();
            if (selectedItem != null)
            {
                action(selectedItem);
            }
        }

        private T GetSelectedItem()
        {
            if (_list == null
                || _listView.SelectedIndices.Count != 1)
            {
                return null;
            }

            var itemIndex = _listView.SelectedIndices[0];
            var item = _list[itemIndex];
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
            Console.Out.WriteLine("X02 GetActivateItem AfterActivateIndex" + AfterActivateIndex);
            if (!(AfterActivateIndex >= 0) || _list == null)
            {
                return null;
            }

            var item = _list[AfterActivateIndex];
            AfterActivateIndex = -1;
            return item;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                if (_retrieveVirtualItem != null)
                {
                    _listView.CacheVirtualItems -= MyCacheVirtualItems;
                    _listView.RetrieveVirtualItem -= MyRetrieveVirtualItem;
                }

                if (_columnClick != null)
                {
                    _listView.ColumnClick -= MyColumnClick;
                }

                if (_itemActivate != null)
                {
                    _listView.ItemActivate -= MyItemActivate;
                }

                _contextMenu?.Dispose();
                if (_itemSelectionChanged != null)
                {
                    _listView.SelectedIndexChanged -= MySelectedIndexChanged;
                    _listView.VirtualItemsSelectionRangeChanged -= MyVirtualItemsSelectionRangeChanged;
                }
            }

            isDisposed = true;
        }

        private ListViewItem GetListViewItemAtMouse()
        {
            var mouseLoc = _listView.PointToClient(Control.MousePosition);
            return _listView.GetItemAt(mouseLoc.X, mouseLoc.Y);
        }

        public void SearchListContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var listViewItem = GetListViewItemAtMouse();
            if (listViewItem == null)
            {
                // cancel context menu if no list view item at right click.
                e.Cancel = true;
            }
        }
    }
}