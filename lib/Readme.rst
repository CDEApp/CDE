.. -*- coding: UTF-8 -*-

May 2011 - Version 2.5
----------------------

New features
^^^^^^^^^^^^

* Excel like filtering. Right clicking on a header will show a "Filter" menu, which will allow you to select the values that will survive the filtering.

* `FastDataListView`. Just like a normal `DataListView`, only faster. On my laptop, it comfortably handles datasets of 100,000 rows without trouble. NOTE: This does not virtualize the data access part -- only the UI portion. So, if you have a query that returns one million rows, all the rows will still be loaded from the database. Once loaded, however, they will be managed by a virtual list.

* Fully customizable character map during cell edit mode. 
  This was an overkill solution for the various flavours of "tab wraps to new line" requests.
  As convinence wrappers, `CellEditTabChangesRows` and `CellEditEnterChangesRows` properties have
  been added. 

* Support for VS 2010. The target framework must be a "full" version of .Net. It will not work with a "Client Profile" (which is unfortunately the default for new projects in VS 2010).

* Columns can now disable sorting, grouping, searching and "hide-ability" (`Sortable`, `Groupable` `Searchable` and `Hideable` properties respectively).

Breaking changes
^^^^^^^^^^^^^^^^

* [Medium]: On `VirtualObjectListView`, `DataSource` was renamed to `VirtualListDataSource`. This was necessary to allow FastDataListView which is both a DataListView AND a VirtualListView -- which both used a 'DataSource' property :(

* [Small]: `GetNextItem()` and `GetPreviousItem()` now accept and return `OLVListView` rather than `ListViewItems`.

* [Small]: Renderer for tree column must now be a subclass of `TreeRenderer`, not just a general `IRenderer`

* [Small]: `SelectObject()` and `SelectObjects()` no longer deselect all other rows.
  This gives an much easier way to add objects to the selection. The properties `SelectedObject`
  and `SelectedObjects` *do* still deselect all other rows.

Minor features
^^^^^^^^^^^^^^

* `TextMatchFilter` was seriously reworked. One text filter can now match on multiple strings. `TextMatchFilter` has new factory methods (which make `TextMatchFilter.MatchKind` redundant).

* Revived support for VS 2005 after being provided with a new copy of VS 2005 Express.

* Column selection mechanism can be customised, through the `SelectColumnsOnRightClickBehaviour`. The default is `InlineMenu`, which behaves like previous versions. Other options are `SubMenu` and `ModalDialog`. This required moving the `ColumnSelectionForm` from the demo project into the ObjectListView project.

* Added `OLVColumn.AutoCompleteEditorMode` in preference to `AutoCompleteEditor`  (which is now just a wrapper). Thanks to Clive Haskins 

* Added `ObjectListView.IncludeColumnHeadersInCopy` 

* Added `ObjectListView.Freezing` event
  
* Added `TreeListView.ExpandedObjects` property.

* Added `Expanding`, `Expanded`, `Collapsing` and `Collapsed` events to `TreeListView`.

* Added `ObjectListView.SubItemChecking` event, which is triggered when a checkbox on subitem is checked/unchecked.

* Allow a delegate to owner draw the header

* All model object comparisons now use `Equals()` rather than `==` (thanks to vulkanino)

* Tweaked `UseTranslucentSelection` and `UseTranslucentHotItem` to look (a little) more like Vista/Win7.

* Added ability to have a gradient background on `BorderDecoration`

* Ctrl-C copying is now able to use the `DragSource` to create the data transfer object (controlled via `CopySelectionOnControlCUsesDragSource` property).

* While editing a cell, `Alt-[arrow]` will try to edit the cell in that direction
  (showing off what the cell edit character mapping can achieve)

* Added long, :ref:`tutorial-like walk-through <blog-rearrangingtreelistview>` of how to make a `TreeListView` rearrangeable.

* Reorganized files into folders
