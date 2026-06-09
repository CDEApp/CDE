using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using cdeWin.Cfg;

namespace cdeWin;

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
    private readonly ContextMenuStrip _menu = new() {ShowCheckMargin = false, ShowImageMargin = false};

    private bool _isDisposed;
    private readonly ToolStripMenuItem _viewTree = new("View Tree");
    private readonly ToolStripMenuItem _open = new("Open");
    private readonly ToolStripMenuItem _explore = new("Explore");

    private readonly ToolStripMenuItem _properties = new("Properties"); // like explorer

    private readonly ToolStripMenuItem _selectAll = new("Select All");

    private readonly ToolStripMenuItem _copyFullName = new("Copy Full Path to Clipboard");

    private readonly ToolStripMenuItem
        _parent = new("Parent"); // for Directory list view parent ? not useful SearchResult

    public EventHandler TreeViewHandler
    {
        get => _viewTreeHandler;
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
        get => _openHandler;
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
        get => _exploreHandler;
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
        get => _propertiesHandler;
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
        get => _selectAllHandler;
        set
        {
            _selectAllHandler = value;
            _selectAll.Click += _selectAllHandler;
            _menu.Items.Add(_selectAll);
        }
    }

    private EventHandler _selectAllHandler;

    public EventHandler CopyFullNameHandler
    {
        get => _copyFullNameHandler;
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
        get => _parentHandler;
        set
        {
            _parentHandler = value;
            _parent.Click += _parentHandler;
            _menu.Items.Add(_parent);
        }
    }

    private EventHandler _parentHandler;

    private readonly List<ToolStripMenuItem> _customItems = new();

    /// <summary>
    /// Append one menu item per configured custom command (its definition carried in
    /// <see cref="ToolStripItem.Tag"/>), all routed through a single click handler. Items are
    /// dynamic, so unlike the fixed handlers they are not part of the passive-view event convention.
    /// </summary>
    public void AddCustomCommands(IEnumerable<CustomCommandOptions> commands, Action<CustomCommandOptions> onClick)
    {
        if (commands == null) return;

        var addedSeparator = false;
        foreach (var command in commands)
        {
            if (!addedSeparator && _menu.Items.Count > 0)
            {
                _menu.Items.Add(new ToolStripSeparator());
                addedSeparator = true;
            }

            var item = new ToolStripMenuItem(command.Label) { Tag = command };
            item.Click += (s, _) => onClick((CustomCommandOptions)((ToolStripMenuItem)s).Tag);
            _customItems.Add(item);
            _menu.Items.Add(item);
        }
    }

    /// <summary>
    /// Set Opening event handler for context menu.
    /// </summary>
    public CancelEventHandler CancelOpeningEventHandler
    {
        get;
        set
        {
            field = value;
            _menu.Opening += value;
        }
    }

    public ContextMenuHelper()
    {
        // set all keys here rather than in individual setters for handlers.
        _viewTree.ShortcutKeyDisplayString = "Enter"; // for documentation of ItemActivate which is Enter.
        _open.ShortcutKeys = Keys.Control | Keys.Enter;
        _explore.ShortcutKeys = Keys.Control | Keys.E;
        _properties.ShortcutKeys = Keys.Alt | Keys.Enter;
        _selectAll.ShortcutKeys = Keys.Control | Keys.A;
        _parent.ShortcutKeys = Keys.Control | Keys.Back;
        _copyFullName.ShortcutKeys = Keys.Control | Keys.C;
    }

    public ContextMenuStrip GetContextMenuStrip()
    {
        foreach (var item in _menu.Items)
        {
            // Skip separators (added by custom commands) - only menu items carry these properties.
            if (item is not ToolStripMenuItem menuItem) continue;
            menuItem.ShowShortcutKeys = true;
            menuItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
        }

        return _menu;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
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

            if (_copyFullNameHandler != null)
            {
                _copyFullName.Click -= _copyFullNameHandler;
            }

            if (_parentHandler != null)
            {
                _parent.Click += _parentHandler;
            }

            foreach (var customItem in _customItems)
            {
                customItem.Dispose();
            }

            _copyFullName.Dispose();
            _explore.Dispose();
            _menu.Dispose();
            _open.Dispose();
            _parent.Dispose();
            _properties.Dispose();
            _selectAll.Dispose();
            _viewTree.Dispose();
        }

        _isDisposed = true;
    }
}