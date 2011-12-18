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
    /// Only ListView events required are enabled.
    /// Several property setters add Event as required so dont call them more than once.
    /// </summary>
    public class ListViewHelper
    {
        private readonly ListView _listView;
        // very simple caching of LVI's, just remembers the previous index - its a big win for how simple.
        private ListViewItem _cacheListViewItem;
        private int _cacheIndex;
        //private int _skippedRVI = 0;      // stats on caching just to get a feel.
        //private int _calledRVI = 0;

        public int ItemIndex { get; set; }
        public ListViewItem RenderItem { get; set; }
        public int AfterActivateIndex { get; set; }
        public int ColumnClickIndex { get; set; }
        public IEnumerable<int> SelectedIndices { get; set; }
        public int SelectedIndicesCount { get; set; }

        /// <summary>
        /// The item under the mouse when context menu launched, ie right mouse button up.
        /// </summary>
        public int ContextItemIndex { get; set; }

        public ListViewHelper(ListView listView)
        {
            _listView = listView;
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
        }

        /// <summary>
        /// Adds CacheVirtualItems, RetrieveVirtualItem handler which sets ItemIndex before EventAction.
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
            //_calledRVI++;
            if (_cacheIndex != itemIndex)
            {
                ItemIndex = itemIndex;
                _retrieveVirtualItem();
                _cacheIndex = itemIndex;
                _cacheListViewItem = RenderItem;
                //if (_cacheIndex == 0)
                //{
                //    Console.WriteLine("moo");
                //    _skippedRVI = 0;
                //    _calledRVI = 0;
                //}            
            }
            //else
            //{
            //    _skippedRVI++;
            //}
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
    }
}