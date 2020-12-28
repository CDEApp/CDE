using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using cdeLib;
using cdeLib.Entities;
using Util;

namespace cdeWin
{
    /* Passive view hackery http://cre8ivethought.com/blog/2009/12/19/using-conventions-with-passive-view 
     * This is ok, but its one presenter per Form, lots of controls gets large...
     * Would like to break up into separate presenters for sets of controls and maybe a higher level
     * coordinating presenter.
     * 
     * Alternate presenter setup in cdeWinForm - need to review it again.
     */

    public partial class CDEWinForm : Form, ICDEWinForm
    {
        private const int DirectoryTabIndex = 2;

        public event EventAction OnFormShown;
        public event EventAction OnFormActivated;
        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearch;
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogRetrieveVirtualItem;
        public event EventAction OnExitMenuItem;
        public event EventAction OnAboutMenuItem;

        public event EventAction OnSearchResultContextMenuViewTreeClick;
        public event EventAction OnSearchResultContextMenuOpenClick;
        public event EventAction OnSearchResultContextMenuExploreClick;
        public event EventAction OnSearchResultContextMenuPropertiesClick;
        public event EventAction OnSearchResultContextMenuSelectAllClick;
        public event EventAction OnSearchResultContextMenuCopyFullPathClick;

        public event EventAction OnDirectoryContextMenuViewTreeClick;
        public event EventAction OnDirectoryContextMenuOpenClick;
        public event EventAction OnDirectoryContextMenuExploreClick;
        public event EventAction OnDirectoryContextMenuPropertiesClick;
        public event EventAction OnDirectoryContextMenuSelectAllClick;
        public event EventAction OnDirectoryContextMenuCopyFullPathClick;
        public event EventAction OnDirectoryContextMenuParentClick;

        public event EventAction OnDirectoryRetrieveVirtualItem;
        public event EventAction OnDirectoryListViewItemActivate;
        public event EventAction OnDirectoryListViewColumnClick;
        public event EventAction OnDirectoryListViewItemSelectionChanged;

        public event EventAction OnSearchResultRetrieveVirtualItem;
        public event EventAction OnSearchResultListViewItemActivate;
        public event EventAction OnSearchResultListViewColumnClick;

        public event EventAction OnCatalogListViewItemActivate;
        public event EventAction OnCatalogListViewColumnClick;

        public event EventAction OnAdvancedSearchCheckboxChanged;

        public event EventAction OnDirectoryTreeContextMenuOpenClick;
        public event EventAction OnDirectoryTreeContextMenuExploreClick;
        public event EventAction OnDirectoryTreeContextMenuPropertiesClick;

        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        public IListViewHelper<PairDirEntry> SearchResultListViewHelper { get; set; }
        public IListViewHelper<ICommonEntry> DirectoryListViewHelper { get; set; }
        public IListViewHelper<RootEntry> CatalogListViewHelper { get; set; }

        public CheckBoxDependentControlHelper FromDate { get; set; }
        public CheckBoxDependentControlHelper ToDate { get; set; }
        public CheckBoxDependentControlHelper FromHour { get; set; }
        public CheckBoxDependentControlHelper ToHour { get; set; }
        public CheckBoxDependentControlHelper FromSize { get; set; }
        public CheckBoxDependentControlHelper ToSize { get; set; }
        public CheckBoxDependentControlHelper NotOlderThan { get; set; }

        public DropDownHelper<int> LimitResultHelper { get; set; }
        public DropDownHelper<int> FromSizeDropDownHelper { get; set; }
        public DropDownHelper<int> ToSizeDropDownHelper { get; set; }
        public DropDownHelper<AddTimeUnitFunc> NotOlderThanDropDownHelper { get; set; }

        public UpDownHelper FromSizeValue { get; set; }
        public UpDownHelper ToSizeValue { get; set; }
        public UpDownHelper NotOlderThanValue { get; set; }

        public event EventAction OnReloadCatalogs;

        private readonly ComboBoxItem<int>[] _byteSizeUnits = new[]
        {
            new ComboBoxItem<int>("byte(s)", 1),
            new ComboBoxItem<int>("KB(s)", 1000),
            new ComboBoxItem<int>("KiB(s)", 1024),
            new ComboBoxItem<int>("MB(s)", 1000 * 1000),
            new ComboBoxItem<int>("MiB(s)", 1024 * 1024),
            new ComboBoxItem<int>("GB(s)", 1000 * 1000 * 1000),
            new ComboBoxItem<int>("GIB(s)", 1024 * 1024 * 1024)
        };

        private readonly ComboBoxItem<AddTimeUnitFunc>[] _durationUnits = new[]
        {
            new ComboBoxItem<AddTimeUnitFunc>("Minute(s)", AddTimeUtil.AddMinute),
            new ComboBoxItem<AddTimeUnitFunc>("Hour(s)", AddTimeUtil.AddHour),
            new ComboBoxItem<AddTimeUnitFunc>("Day(s)", AddTimeUtil.AddDay),
            new ComboBoxItem<AddTimeUnitFunc>("Month(s)", AddTimeUtil.AddMonth),
            new ComboBoxItem<AddTimeUnitFunc>("Year(s)", AddTimeUtil.AddYear)
        };

        private readonly ComboBoxItem<int>[] _limitResultValues = new[]
        {
            new ComboBoxItem<int>("Max Results 1000", 1000),
            new ComboBoxItem<int>("Max Results 10000", 10000),
            new ComboBoxItem<int>("Max Results 100000", 100000),
            new ComboBoxItem<int>("Unlimited Results", int.MaxValue)
        };

        private readonly IConfig _config;

        public CDEWinForm(IConfig config)
        {
            _config = config;
            InitializeComponent();
            AutoWaitCursor.Cursor = Cursors.WaitCursor;
            AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
            AutoWaitCursor.MainWindowHandle = Handle;
            AutoWaitCursor.Start();
            RegisterClientEvents();
        }

        public void CleanUp()
        {
            SearchResultListViewHelper.Dispose();
            DirectoryListViewHelper.Dispose();
            CatalogListViewHelper.Dispose();
            UnregisterClientEvents();
        }

        public void UnregisterClientEvents()
        {
            FormClosing -= MyFormClosing;
            Activated -= MyFormActivated;
            directoryTreeView.BeforeExpand -= DirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect -= DirectoryTreeViewOnAfterSelect;
        }

        private void RegisterClientEvents()
        {
            FormClosing += MyFormClosing;
            Activated += MyFormActivated;

            SetToolTip(reloadCatalogsButton,
                "Using reload catalogs will use more memory than quitting and starting again.");

            SetToolTip(regexCheckbox, "Disabling Regex makes search faster");
            whatToSearchComboBox.Items.AddRange(new object[] { "Include Path in Search", "Exclude Path from Search" });
            whatToSearchComboBox.SelectedIndex = 0; // default Include
            whatToSearchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            SetToolTip(whatToSearchComboBox,
                "Excluding Path so that only entry Names are searched makes search faster.");

            findComboBox.Items.AddRange(new object[] { "Files and Folders", "Files Only", "Folders Only" });
            findComboBox.SelectedIndex = 0; // default Files and Folders
            findComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

            // TODO having ListViewHelper setup in VIEW breaks passive view. i think.
            // * it does register a bunch of events which it fires.... ? so not real bad.
            // - whats happening is im making view smarter... with specific behaviour.
            // - but its not passive, passive would require ListViewHelper to raise events
            // - from gui actions....  and decisions from presenter...
            // - - at moment, ListViewHelper is small presenter ?
            SearchResultListViewHelper = new ListViewHelper<PairDirEntry>(searchResultListView)
            {
                // ReSharper disable PossibleNullReferenceException
                RetrieveVirtualItem = () => OnSearchResultRetrieveVirtualItem(),
                ItemActivate = () => OnSearchResultListViewItemActivate(),
                ColumnClick = () => OnSearchResultListViewColumnClick(),
                // ReSharper restore PossibleNullReferenceException
                ContextMenu = CreateSearchResultContextMenu(),
            };

            DirectoryListViewHelper = new ListViewHelper<ICommonEntry>(directoryListView)
            {
                // ReSharper disable PossibleNullReferenceException
                RetrieveVirtualItem = () => { OnDirectoryRetrieveVirtualItem(); },
                ItemActivate = () => { OnDirectoryListViewItemActivate(); },
                ColumnClick = () => { OnDirectoryListViewColumnClick(); },
                ItemSelectionChanged = () => { OnDirectoryListViewItemSelectionChanged(); },
                // ReSharper restore PossibleNullReferenceException
                ContextMenu = CreateDirectoryContextMenu(),
            };

            directoryTreeView.BeforeExpand += DirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect += DirectoryTreeViewOnAfterSelect;
            directoryTreeView.ContextMenuStrip = CreateDirectoryTreeContextMenu();

            // Enter in pattern Text Box fires Search Button.
            // TODO move this logic into presenter... not here..
            // means fire event for each of GotFocus, LostFocus. and handle in presenter.
            patternComboBox.GotFocus += (s, e) => AcceptButton = searchButton;
            patternComboBox.LostFocus += (s, e) => AcceptButton = null;

            CatalogListViewHelper = new ListViewHelper<RootEntry>(catalogResultListView)
            {
                MultiSelect = false,
                // ReSharper disable PossibleNullReferenceException
                RetrieveVirtualItem = () => { OnCatalogRetrieveVirtualItem(); },
                ItemActivate = () => { OnCatalogListViewItemActivate(); },
                ColumnClick = () => { OnCatalogListViewColumnClick(); },
                // ReSharper restore PossibleNullReferenceException
            };

            directoryPathTextBox.ReadOnly = true; // only for display and manual select copy for now ?

            // ReSharper disable PossibleNullReferenceException
            exitToolStripMenuItem.Click += (s, e) => OnExitMenuItem();
            aboutToolStripMenuItem.Click += (s, e) => OnAboutMenuItem();

            searchButton.Click += (s, e) => OnSearch();
            // ReSharper restore PossibleNullReferenceException
            SetToolTip(searchButton, "Cancel Search is not immediate, wait for a progress update.");

            // ReSharper disable once PossibleNullReferenceException
            reloadCatalogsButton.Click += (s, e) => OnReloadCatalogs();

            RegisterAdvancedSearchControls();
        }

        // ReSharper disable UseObjectOrCollectionInitializer
        private void RegisterAdvancedSearchControls()
        {
            SetTimePickerYMD(fromDateTimePicker);
            // TODO - all these CheckBoxDependentControlHelper 
            // - are breaking the passive view model.
            // - logic needs to be in presenter so these need to event.
            // - it the handlers should be raising events to Presenter. 
            // - or maybe some presenter of presenter just for checkbox dependencies ?
            //
            FromDate = new CheckBoxDependentControlHelper(fromDateCheckbox, new Control[] { fromDateTimePicker },
                new[] { notOlderThanCheckbox })
            {
                Checked = false
            };

            SetTimePickerYMD(toDateTimePicker);
            ToDate = new CheckBoxDependentControlHelper(toDateCheckbox, new Control[] { toDateTimePicker },
                new[] { notOlderThanCheckbox })
            {
                Checked = false
            };

            SetTimePickerHMS(fromHourTimePicker);
            FromHour = new CheckBoxDependentControlHelper(fromHourCheckbox, new Control[] { fromHourTimePicker },
                new[] { notOlderThanCheckbox })
            {
                Checked = false
            };

            SetTimePickerHMS(toHourTimePicker);
            ToHour = new CheckBoxDependentControlHelper(toHourCheckbox, new Control[] { toHourTimePicker },
                new[] { notOlderThanCheckbox })
            {
                Checked = false
            };

            NotOlderThanValue = new UpDownHelper(notOlderThanUpDown, 0);
            NotOlderThanDropDownHelper = new DropDownHelper<AddTimeUnitFunc>(notOlderThanDropDown, _durationUnits, 1);

            NotOlderThan = new CheckBoxDependentControlHelper(notOlderThanCheckbox,
                new Control[] { notOlderThanUpDown, notOlderThanDropDown },
                new[] { fromDateCheckbox, toDateCheckbox, fromHourCheckbox, toHourCheckbox })
            {
                Checked = false
            };

            FromSizeValue = new UpDownHelper(fromSizeUpDown);
            FromSizeDropDownHelper = new DropDownHelper<int>(fromSizeDropDown, _byteSizeUnits, 4);
            FromSize = new CheckBoxDependentControlHelper(fromSizeCheckbox,
                new Control[] { fromSizeUpDown, fromSizeDropDown }, null)
            {
                Checked = false
            };

            ToSizeValue = new UpDownHelper(toSizeUpDown);
            ToSizeDropDownHelper = new DropDownHelper<int>(toSizeDropDown, _byteSizeUnits, 4);
            ToSize = new CheckBoxDependentControlHelper(toSizeCheckbox, new Control[] { toSizeUpDown, toSizeDropDown },
                null)
            {
                Checked = false
            };

            LimitResultHelper = new DropDownHelper<int>(limitResultDropDown, _limitResultValues, 1);
            SetToolTip(limitResultDropDown,
                "Recommend 10000 or smaller. Producing very large result lists uses a lot of memory and isn't usually useful.");

            // ReSharper disable once PossibleNullReferenceException
            advancedSearchCheckBox.CheckedChanged += (s, e) => OnAdvancedSearchCheckboxChanged();
            SetToolTip(advancedSearchCheckBox,
                "Enable or Disable advanced search options to include Date and Size filtering.");
        }
        // ReSharper restore UseObjectOrCollectionInitializer

        private void SetTimePickerHMS(DateTimePicker picker)
        {
            picker.ShowUpDown = true;
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = _config.DateCustomFormatHMS;
        }

        private void SetTimePickerYMD(DateTimePicker picker)
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = _config.DateCustomFormatYMD;
        }

        private void SetToolTip(Control c, string s)
        {
            var tip = new ToolTip();
            tip.SetToolTip(c, s);
        }

        private void MyFormClosing(object s, FormClosingEventArgs e)
        {
            // ReSharper disable once PossibleNullReferenceException
            OnMyFormClosing();
        }

        private ContextMenuStrip CreateDirectoryTreeContextMenu()
        {
            var menuHelper = new ContextMenuHelper
            {
                //TreeViewHandler not useful in tree
                // ReSharper disable PossibleNullReferenceException
                OpenHandler = (s, e) => OnDirectoryTreeContextMenuOpenClick(),
                ExploreHandler = (s, e) => OnDirectoryTreeContextMenuExploreClick(),
                PropertiesHandler = (s, e) => OnDirectoryTreeContextMenuPropertiesClick(),
                //SelectAllHandler = not useful in tree
                //CopyBaseNameHandler = (s, e) => (),
                //CopyFullNameHandler = (s, e) => (),
                ParentHandler = (s, e) => OnDirectoryContextMenuParentClick(),
                // ReSharper restore PossibleNullReferenceException
                CancelOpeningEventHandler = (s, e) => DirectoryTreeContextMenuOpening(s, e),
            };

            return menuHelper.GetContextMenuStrip();
        }

        // ReSharper disable once UnusedParameter.Local
        private void DirectoryTreeContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ReSharper disable once ArrangeStaticMemberQualifier
            var treeNodeAtMousePosition =
                directoryTreeView.GetNodeAt(directoryTreeView.PointToClient(Control.MousePosition));
            var selectedTreeNode = directoryTreeView.SelectedNode;

            if (treeNodeAtMousePosition == null)
            {
                // cancel context menu if no node at right click.
                e.Cancel = true;
            }
            else
            {
                // right click selects the node and shows context menu.
                if (treeNodeAtMousePosition != selectedTreeNode)
                    directoryTreeView.SelectedNode = treeNodeAtMousePosition;
            }
        }

        public ICommonEntry GetSelectedTreeItem()
        {
            // any visible tree node has a valid Tag
            return (ICommonEntry)directoryTreeView.SelectedNode?.Tag;
        }

        private ContextMenuStrip CreateDirectoryContextMenu()
        {
            var menuHelper = new ContextMenuHelper
            {
                // ReSharper disable PossibleNullReferenceException
                TreeViewHandler = (s, e) => OnDirectoryContextMenuViewTreeClick(),
                OpenHandler = (s, e) => OnDirectoryContextMenuOpenClick(),
                ExploreHandler = (s, e) => OnDirectoryContextMenuExploreClick(),
                PropertiesHandler = (s, e) => OnDirectoryContextMenuPropertiesClick(),
                SelectAllHandler = (s, e) => OnDirectoryContextMenuSelectAllClick(),
                //CopyBaseNameHandler = (s, e) => (),
                CopyFullNameHandler = (s, e) => OnDirectoryContextMenuCopyFullPathClick(),
                ParentHandler = (s, e) => OnDirectoryContextMenuParentClick(),
                // ReSharper restore PossibleNullReferenceException
                CancelOpeningEventHandler = (s, e) => DirectoryListViewHelper.SearchListContextMenuOpening(s, e),
            };
            return menuHelper.GetContextMenuStrip();
        }

        private ContextMenuStrip CreateSearchResultContextMenu()
        {
            var menuHelper = new ContextMenuHelper
            {
                // ReSharper disable PossibleNullReferenceException
                TreeViewHandler = (s, e) => OnSearchResultContextMenuViewTreeClick(),
                OpenHandler = (s, e) => OnSearchResultContextMenuOpenClick(),
                ExploreHandler = (s, e) => OnSearchResultContextMenuExploreClick(),
                PropertiesHandler = (s, e) => OnSearchResultContextMenuPropertiesClick(),
                SelectAllHandler = (s, e) => OnSearchResultContextMenuSelectAllClick(),
                //CopyBaseNameHandler = (s, e) => (),
                CopyFullNameHandler = (s, e) => OnSearchResultContextMenuCopyFullPathClick(),
                // ReSharper restore PossibleNullReferenceException
                CancelOpeningEventHandler = (s, e) => SearchResultListViewHelper.SearchListContextMenuOpening(s, e),
            };
            return menuHelper.GetContextMenuStrip();
        }

        private void MyFormActivated(object sender, EventArgs eventArgs)
        {
            // ReSharper disable once PossibleNullReferenceException
            OnFormActivated();
        }

        private void DirectoryTreeViewOnAfterSelect(object s, TreeViewEventArgs e)
        {
            DirectoryTreeViewActiveAfterSelectNode = e.Node;
            // ReSharper disable once PossibleNullReferenceException
            OnDirectoryTreeViewAfterSelect();
        }

        private void DirectoryTreeViewOnBeforeExpand(object s, TreeViewCancelEventArgs e)
        {
            DirectoryTreeViewActiveBeforeExpandNode = e.Node;
            // ReSharper disable once PossibleNullReferenceException
            OnDirectoryTreeViewBeforeExpandNode();
        }

        public string Pattern
        {
            get => patternComboBox.Text;
            set => patternComboBox.Text = value;
        }

        public bool RegexMode
        {
            get => regexCheckbox.Checked;
            set => regexCheckbox.Checked = value;
        }

        /// <summary>
        /// Adds nodes to tree wrapped by BeginUpdate EndUpdate.
        /// </summary>
        public TreeNode DirectoryTreeViewNodes
        {
            // Directory Tab only holds one root node.
            get => directoryTreeView.Nodes.Count == 1 ? directoryTreeView.Nodes[0] : null;

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

        public bool IncludeFiles => findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 1;

        public bool IncludeFolders => findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 2;

        public int FindEntryFilter
        {
            get => findComboBox.SelectedIndex;
            set => findComboBox.SelectedIndex = value;
        }

        public bool IncludePathInSearch
        {
            get => whatToSearchComboBox.SelectedIndex == 0;
            set => whatToSearchComboBox.SelectedIndex = value ? 0 : 1;
        }

        public void SetSearchResultStatus(int i)
        {
            searchResultsStatus.Text = "Search Results " + i.ToString(CultureInfo.InvariantCulture);
        }

        public void SetTotalFileEntriesLoadedStatus(int i)
        {
            totalFileEntriesStatus.Text = "Entries " + i.ToString(CultureInfo.InvariantCulture);
        }

        public void SetCatalogsLoadedStatus(int i)
        {
            catalogsLoadedStatus.Text = "Catalogs " + i.ToString(CultureInfo.InvariantCulture);
        }

        public void SetSearchTimeStatus(string s)
        {
            if (searchTimeStatus.Text.Equals(s)) return;
            searchTimeStatus.Text = s;
            this.mainStatusStrip.Update(); //Added since it is not updating this automagically.
        }

        public void SetSearchTextBoxAutoComplete(IEnumerable<string> history)
        {
            patternComboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            patternComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            if (history != null)
            {
                foreach (var h in history.Where(h => !string.IsNullOrEmpty(h)))
                {
                    patternComboBox.Items.Add(h);
                }
            }

            patternComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        }

        public void AddSearchTextBoxAutoComplete(string pattern)
        {
            if (!string.IsNullOrEmpty(pattern.Trim()))
            {
                var items = patternComboBox.Items;
                if (items.Contains(pattern))
                {
                    items.Remove(pattern);
                }

                items.TruncateList(_config.Active.PatternHistoryMaximum);
                items.Insert(0, pattern); // always to front.
                patternComboBox.SelectedIndex = 0; // set value we added to combobox.
                patternComboBox.Select(0, pattern.Length);
            }
        }

        public List<string> GetSearchTextBoxAutoComplete()
        {
            return patternComboBox
                .Items
                .Cast<string>()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        public void SelectDirectoryPane()
        {
            // ReSharper disable RedundantCheckBeforeAssignment
            if (mainTabControl.SelectedIndex != DirectoryTabIndex)
            {
                mainTabControl.SelectedIndex = DirectoryTabIndex;
            }
            // ReSharper restore RedundantCheckBeforeAssignment
        }

        public float DirectoryPanelSplitterRatio
        {
            get => directorySplitContainer.GetSplitterRatio();
            set => directorySplitContainer.SetSplitterRatio(value);
        }

        public string SetDirectoryPathTextBox
        {
            get => directoryPathTextBox.Text;
            set => directoryPathTextBox.Text = value;
        }

        public TreeNode DirectoryTreeViewSelectedNode
        {
            get => directoryTreeView.SelectedNode;
            set => directoryTreeView.SelectedNode = value;
        }

        public bool SearchButtonEnable
        {
            get => searchButton.Enabled;
            set => searchButton.Enabled = value;
        }

        public string SearchButtonText
        {
            get => searchButton.Text;
            set => searchButton.Text = value;
        }

        public Color SearchButtonBackColor
        {
            get => searchButton.BackColor;
            set => searchButton.BackColor = value;
        }

        public bool IsAdvancedSearchMode
        {
            get => advancedSearchCheckBox.Checked;
            set
            {
                advancedSearchCheckBox.Checked = value;
                searchControlAdvancedPanel.Visible = value;
                searchControlAdvancedPanel.Enabled = value;
            }
        }

        public DateTime FromDateValue
        {
            get => fromDateTimePicker.Value;
            set => fromDateTimePicker.Value = value;
        }

        public DateTime ToDateValue
        {
            get => toDateTimePicker.Value;
            set => toDateTimePicker.Value = value;
        }

        public DateTime FromHourValue
        {
            get => fromHourTimePicker.Value;
            set => fromHourTimePicker.Value = value;
        }

        public DateTime ToHourValue
        {
            get => toHourTimePicker.Value;
            set => toHourTimePicker.Value = value;
        }

        public void MessageBox(string message)
        {
            MyMessageBox.MyShow(this, message);
        }

        public void AboutDialog()
        {
            MyAboutBox.MyShow(this, _config);
        }

        public void AddLine(string format, params object[] args)
        {
            tbLog.AppendText(string.Format(format, args) + Environment.NewLine);
        }

        private void CDEWinFormShown(object sender, EventArgs e)
        {
            // ReSharper disable once PossibleNullReferenceException
            OnFormShown();
        }
    }
}