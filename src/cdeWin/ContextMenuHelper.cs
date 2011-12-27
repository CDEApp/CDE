using System;
using System.Windows.Forms;

namespace cdeWin
{
    /// <summary>
    /// Context Menu Strip creation for fixed names with configurable handlers.
    /// Only those handlers that are set will appear in Menu.
    /// Usage:
    /// 
    /// var helper = new ContextMenuHelper
    /// {
    ///   TreeViewHandler = MyTreeViewHandler,
    ///   OpenHandler = MyOpenHandler,
    /// };
    /// var myContextMenuStrip = helper.GetContextMenuStrip();
    /// 
    /// </summary>
    public class ContextMenuHelper : IDisposable
    {
        // Fields set on ContextMenuStrip remove space for icons on left of menu for items.
        private readonly ContextMenuStrip _menu = new ContextMenuStrip { ShowCheckMargin = false, ShowImageMargin = false };

        private readonly ToolStripMenuItem _viewTree = new ToolStripMenuItem("View Tree");
        private readonly ToolStripMenuItem _open = new ToolStripMenuItem("Open");
        private readonly ToolStripMenuItem _explore = new ToolStripMenuItem("Explore");
        private readonly ToolStripMenuItem _properties = new ToolStripMenuItem("Properties"); // like explorer -- TODO in treeview to ?
        private readonly ToolStripMenuItem _selectAll = new ToolStripMenuItem("Select All");
        private readonly ToolStripMenuItem _copyBaseName = new ToolStripMenuItem("Copy Base Names");
        private readonly ToolStripMenuItem _copyFullName = new ToolStripMenuItem("Copy Full Qualified Name");
        private readonly ToolStripMenuItem _parent = new ToolStripMenuItem("Parent"); // for Directory listview parent ? not useful SearchResult

        public EventHandler TreeViewHandler
        {
            get { return _viewTreeHandler; }
            set
            {
                _viewTreeHandler = value;
                _viewTree.Click += _viewTreeHandler;
                _menu.Items.Add(_viewTree);
            }
        }
        private EventHandler _viewTreeHandler;

        public EventHandler OpenHandler
        {
            get { return _openHandler; }
            set
            {
                _openHandler = value;
                _open.Click += _openHandler;
                _menu.Items.Add(_open);
            }
        }
        private EventHandler _openHandler;

        public EventHandler ExploreHandler
        {
            get { return _exploreHandler; }
            set
            {
                _exploreHandler = value;
                _explore.Click += _exploreHandler;
                _menu.Items.Add(_explore);
            }
        }
        private EventHandler _exploreHandler;

        public EventHandler PropertiesHandler
        {
            get { return _propertiesHandler; }
            set
            {
                _propertiesHandler = value;
                _properties.Click += _propertiesHandler;
                _menu.Items.Add(_properties);
            }
        }
        private EventHandler _propertiesHandler;

        public EventHandler SelectAllHandler
        {
            get { return _selectAllHandler; }
            set
            {
                _selectAllHandler = value;
                _selectAll.Click += _selectAllHandler;
                _menu.Items.Add(_selectAll);
            }
        }
        private EventHandler _selectAllHandler;

        public EventHandler CopyBaseNameHandler
        {
            get { return _copyBaseNameHandler; }
            set
            {
                _copyBaseNameHandler = value;
                _copyBaseName.Click += _copyBaseNameHandler;
                _menu.Items.Add(_copyBaseName);
            }
        }
        private EventHandler _copyBaseNameHandler;

        public EventHandler CopyFullNameHandler
        {
            get { return _copyFullNameHandler; }
            set
            {
                _copyFullNameHandler = value;
                _copyFullName.Click += _copyFullNameHandler;
                _menu.Items.Add(_copyFullName);
            }
        }
        private EventHandler _copyFullNameHandler;

        public EventHandler ParentHandler
        {
            get { return _parentHandler; }
            set
            {
                _parentHandler = value;
                _parent.Click += _parentHandler;
                _menu.Items.Add(_parent);
            }
        }
        private EventHandler _parentHandler;

        public ContextMenuHelper()
        {   // set all keys here rather than in individual setters for handlers.
            _viewTree.ShortcutKeyDisplayString = "Enter"; // for documentation of ItemActivate which is Enter.
            _open.ShortcutKeys = Keys.Control | Keys.Enter;
            _explore.ShortcutKeys = Keys.Control | Keys.E;
            _properties.ShortcutKeys = Keys.Alt | Keys.Enter;
            _selectAll.ShortcutKeys = Keys.Control | Keys.A;
            _parent.ShortcutKeys = Keys.Control | Keys.Back;
            _copyBaseName.ShortcutKeys = Keys.Control | Keys.N;
            _copyFullName.ShortcutKeys = Keys.Control | Keys.C;
        }

        public ContextMenuStrip GetContextMenuStrip()
        {
            foreach (ToolStripMenuItem menuItem in _menu.Items)
            {
                menuItem.ShowShortcutKeys = true;
                menuItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            }
            return _menu;
        }

        public void Dispose()
        {
            if (_viewTreeHandler != null)
            {
                _viewTree.Click -= _viewTreeHandler;
            }
            if (_openHandler != null)
            {
                _open.Click -= _openHandler;
            }
            if (_exploreHandler != null)
            {
                _explore.Click -= _exploreHandler;
            }
            if (_propertiesHandler != null)
            {
                _properties.Click -= _propertiesHandler;
            }
            if (_selectAllHandler != null)
            {
                _selectAll.Click -= _selectAllHandler;
            }
            if (_copyBaseNameHandler != null)
            {
                _copyBaseName.Click -= _copyBaseNameHandler;
            }
            if (_copyFullNameHandler != null)
            {
                _copyFullName.Click -= _copyFullNameHandler;
            }
            if (_parentHandler != null)
            {
                _parent.Click += _parentHandler;
            }
        }
    }
}