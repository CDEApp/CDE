using System;
using System.Collections.Generic;
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

    public partial class CDEWinForm : Form, ICDEWinForm
    {
        private const int DirectoryTabIndex = 2;

        public event EventAction OnDirectoryTreeViewBeforeExpandNode;
        public event EventAction OnDirectoryTreeViewAfterSelect;
        public event EventAction OnSearch;
        public event EventAction OnMyFormClosing;
        public event EventAction OnCatalogRetrieveVirtualItem;
        public event EventAction OnExitMenuItem;
        public event EventAction OnCancelSearch;

        public event EventAction OnSearchResultContextMenuViewTreeClick;
        public event EventAction OnSearchResultContextMenuOpenClick;
        public event EventAction OnSearchResultContextMenuExploreClick;
        public event EventAction OnSearchResultContextMenuPropertiesClick;
        public event EventAction OnSearchResultContextMenuSelectAllClick;

        public event EventAction OnDirectoryContextMenuViewTreeClick;
        public event EventAction OnDirectoryContextMenuOpenClick;
        public event EventAction OnDirectoryContextMenuExploreClick;
        public event EventAction OnDirectoryContextMenuPropertiesClick;
        public event EventAction OnDirectoryContextMenuSelectAllClick;
        public event EventAction OnDirectoryContextMenuParentClick;

        public event EventAction OnDirectoryRetrieveVirtualItem;
        public virtual void OnDirectoryRetrieveVirtualItemFire() { OnDirectoryRetrieveVirtualItem(); }
        public event EventAction OnDirectoryListViewItemActivate;
        public virtual void OnDirectoryListViewItemActivateFire() { OnDirectoryListViewItemActivate(); }
        public event EventAction OnDirectoryListViewColumnClick;
        public virtual void OnDirectoryListViewColumnClickFire() { OnDirectoryListViewColumnClick(); }
        public event EventAction OnDirectoryListViewItemSelectionChanged;
        public virtual void OnDirectoryListViewItemSelectionChangedFire() { OnDirectoryListViewItemSelectionChanged(); }

        public event EventAction OnSearchResultRetrieveVirtualItem;
        public virtual void OnSearchResultRetrieveVirtualItemFire() { OnSearchResultRetrieveVirtualItem(); }
        public event EventAction OnSearchResultListViewItemActivate;
        public virtual void OnSearchResultListViewItemActivateFire() { OnSearchResultListViewItemActivate(); }
        public event EventAction OnSearchResultListViewColumnClick;
        public virtual void OnSearchResultListViewColumnClickFire() { OnSearchResultListViewColumnClick(); }

        public virtual void OnCatalogRetrieveVirtualItemFire() { OnCatalogRetrieveVirtualItem(); }
        public event EventAction OnCatalogListViewItemActivate;
        public virtual void OnCatalogListViewItemActivateFire() { OnCatalogListViewItemActivate(); }
        public event EventAction OnCatalogListViewColumnClick;
        public void OnCatalogListViewColumnClickFire() { OnCatalogListViewColumnClick(); }

        public event EventAction OnAdvancedSearchCheckboxChanged;
            
        public TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        public TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        public ListViewHelper<PairDirEntry> SearchResultListViewHelper { get; set; }
        public ListViewHelper<DirEntry> DirectoryListViewHelper { get; set; }
        public ListViewHelper<RootEntry> CatalogListViewHelper { get; set; }

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

        public UpDownHelper<int> FromSizeValue { get; set; }
        public UpDownHelper<int> ToSizeValue { get; set; }
        public UpDownHelper<AddTimeUnitFunc> NotOlderThanValue { get; set; }

        private readonly ComboBoxItem<int>[] _byteSizeUnits = new[]
                {
                    new ComboBoxItem<int>("byte(s)", 1),
                    new ComboBoxItem<int>("KB(s)", 1000),
                    new ComboBoxItem<int>("KiB(s)", 1024),
                    new ComboBoxItem<int>("MB(s)", 1000*1000),
                    new ComboBoxItem<int>("MiB(s)", 1024*1024),
                    new ComboBoxItem<int>("GB(s)", 1000*1000*1000),
                    new ComboBoxItem<int>("GIB(s)", 1024*1024*1024),
                };

        private readonly ComboBoxItem<AddTimeUnitFunc>[] _durationUnits = new[]
                {
                    new ComboBoxItem<AddTimeUnitFunc>("Minute(s)", AddTimeUtil.AddMinute),
                    new ComboBoxItem<AddTimeUnitFunc>("Hour(s)", AddTimeUtil.AddHour),
                    new ComboBoxItem<AddTimeUnitFunc>("Day(s)", AddTimeUtil.AddDay),
                    new ComboBoxItem<AddTimeUnitFunc>("Month(s)", AddTimeUtil.AddMonth),
                    new ComboBoxItem<AddTimeUnitFunc>("Year(s)", AddTimeUtil.AddYear),
                };

        private readonly ComboBoxItem<int>[] _limitResultValues = new[]
                {
                    new ComboBoxItem<int>("Max Results 1000", 1000),
                    new ComboBoxItem<int>("Max Results 10000", 10000),
                    new ComboBoxItem<int>("Max Results 100000", 100000),
                    new ComboBoxItem<int>("Unlimited Results", int.MaxValue),
                };

        public CDEWinForm()
        {
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

            SetToolTip(regexCheckbox, "Disabling Regex makes search faster");
            whatToSearchComboBox.Items.AddRange(new[] { "Include Path in Search", "Exclude Path from Search" });
            whatToSearchComboBox.SelectedIndex = 0; // default Include
            whatToSearchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            SetToolTip(whatToSearchComboBox, "Excluding Path so that only entry Names are searched makes search faster.");

            findComboBox.Items.AddRange(new[] { "Files and Folders", "Files Only", "Folders Only" });
            findComboBox.SelectedIndex = 0; // default Files and Folders
            findComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

            SearchResultListViewHelper = new ListViewHelper<PairDirEntry>(searchResultListView)
                {
                    RetrieveVirtualItem = OnSearchResultRetrieveVirtualItemFire,
                    ItemActivate = OnSearchResultListViewItemActivateFire,
                    ColumnClick = OnSearchResultListViewColumnClickFire,
                    ContextMenu = CreateSearchResultContextMenu(),
                };

            DirectoryListViewHelper = new ListViewHelper<DirEntry>(directoryListView)
                {
                    RetrieveVirtualItem = OnDirectoryRetrieveVirtualItemFire,
                    ItemActivate = OnDirectoryListViewItemActivateFire,
                    ColumnClick = OnDirectoryListViewColumnClickFire,
                    ContextMenu = CreateDirectoryContextMenu(),
                    ItemSelectionChanged = OnDirectoryListViewItemSelectionChangedFire
                };

            directoryTreeView.BeforeExpand += DirectoryTreeViewOnBeforeExpand;
            directoryTreeView.AfterSelect += DirectoryTreeViewOnAfterSelect;

            // Enter in pattern Text Box fires Search Button.
            patternComboBox.GotFocus += (s, e) => AcceptButton = searchButton;
            patternComboBox.LostFocus += (s, e) => AcceptButton = null;
            patternComboBox.DropDownHeight = 1;

            CatalogListViewHelper = new ListViewHelper<RootEntry>(catalogResultListView)
                {
                    MultiSelect = false,
                    RetrieveVirtualItem = OnCatalogRetrieveVirtualItemFire,
                    ItemActivate = OnCatalogListViewItemActivateFire,
                    ColumnClick = OnCatalogListViewColumnClickFire,
                };

            directoryPathTextBox.ReadOnly = true; // only for display and manual select copy for now ?

            exitToolStripMenuItem.Click += (s, e) => OnExitMenuItem();

            searchButton.Click += (s, e) => OnSearch();
            SetToolTip(searchButton, "Cancel Search is not immediate, wait for a progress update.");

            RegisterAdvancedSearchControls();
        }

        // ReSharper disable UseObjectOrCollectionInitializer
        private void RegisterAdvancedSearchControls()
        {
            SetTimePickerYMD(fromDateTimePicker);
            FromDate = new CheckBoxDependentControlHelper(fromDateCheckbox, new Control[] { fromDateTimePicker }, new[] { notOlderThanCheckbox });
            FromDate.Checked = false;

            SetTimePickerYMD(toDateTimePicker);
            ToDate = new CheckBoxDependentControlHelper(toDateCheckbox, new Control[] { toDateTimePicker }, new[] { notOlderThanCheckbox });
            ToDate.Checked = false;

            SetTimePickerHMS(fromHourTimePicker);
            FromHour = new CheckBoxDependentControlHelper(fromHourCheckbox, new Control[] { fromHourTimePicker }, new[] { notOlderThanCheckbox });
            FromHour.Checked = false;

            SetTimePickerHMS(toHourTimePicker);
            ToHour = new CheckBoxDependentControlHelper(toHourCheckbox, new Control[] { toHourTimePicker }, new[] { notOlderThanCheckbox });
            ToHour.Checked = false;

            NotOlderThanValue = new UpDownHelper<AddTimeUnitFunc>(notOlderThanUpDown, 0);
            NotOlderThanDropDownHelper = new DropDownHelper<AddTimeUnitFunc>(notOlderThanDropDown, _durationUnits, 1); // todo config

            NotOlderThan = new CheckBoxDependentControlHelper(notOlderThanCheckbox,
                            new Control[] {notOlderThanUpDown, notOlderThanDropDown},
                            new [] { fromDateCheckbox, toDateCheckbox, fromHourCheckbox, toHourCheckbox });
            NotOlderThan.Checked = false;

            FromSizeValue = new UpDownHelper<int>(fromSizeUpDown);
            FromSizeDropDownHelper = new DropDownHelper<int>(fromSizeDropDown, _byteSizeUnits, 4); // todo config
            FromSize = new CheckBoxDependentControlHelper(fromSizeCheckbox, new Control[] { fromSizeUpDown, fromSizeDropDown }, null);
            FromSize.Checked = false;

            ToSizeValue = new UpDownHelper<int>(toSizeUpDown);
            ToSizeDropDownHelper = new DropDownHelper<int>(toSizeDropDown, _byteSizeUnits, 4); // todo config
            ToSize = new CheckBoxDependentControlHelper(toSizeCheckbox, new Control[] { toSizeUpDown, toSizeDropDown }, null);
            ToSize.Checked = false;

            LimitResultHelper = new DropDownHelper<int>(limitResultDropDown, _limitResultValues, 1);
            SetToolTip(limitResultDropDown, "Recommend 10000 or smaller. Producing very large result lists uses a lot of memory and isnt usually useful.");

            advancedSearchCheckBox.CheckedChanged += (s, e) => OnAdvancedSearchCheckboxChanged();
            SetToolTip(advancedSearchCheckBox, "Enable or Disable advanced search options to include Date and Size filtering.");
        }
        // ReSharper restore UseObjectOrCollectionInitializer

        private void SetTimePickerHMS(DateTimePicker picker)
        {
            picker.ShowUpDown = true;
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = Config.DateCustomFormatHMS;
        }

        private void SetTimePickerYMD(DateTimePicker picker)
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = Config.DateCustomFormatYMD;

        }

        private void SetToolTip(Control c, string s)
        {
            var tip = new ToolTip();
            tip.SetToolTip(c, s);
        }

        private void MyFormClosing(object s, FormClosingEventArgs e)
        {
            OnMyFormClosing();
        }

        private ContextMenuStrip CreateDirectoryContextMenu()
        {
            var menuHelper = new ContextMenuHelper
                {
                    TreeViewHandler = (s, e) => OnDirectoryContextMenuViewTreeClick(),
                    OpenHandler = (s, e) => OnDirectoryContextMenuOpenClick(),
                    ExploreHandler = (s, e) => OnDirectoryContextMenuExploreClick(),
                    PropertiesHandler = (s, e) => OnDirectoryContextMenuPropertiesClick(),
                    SelectAllHandler = (s, e) => OnDirectoryContextMenuSelectAllClick(),
                    //CopyBaseNameHandler = (s, e) => (),
                    //CopyFullNameHandler = (s, e) => (),
                    ParentHandler = (s, e) => OnDirectoryContextMenuParentClick(),
                };
            return menuHelper.GetContextMenuStrip();
        }

        private ContextMenuStrip CreateSearchResultContextMenu()
        {
            var menuHelper = new ContextMenuHelper
            {
                TreeViewHandler = (s,e) => OnSearchResultContextMenuViewTreeClick(),
                OpenHandler = (s, e) => OnSearchResultContextMenuOpenClick(),
                ExploreHandler = (s, e) => OnSearchResultContextMenuExploreClick(),
                PropertiesHandler = (s, e) => OnSearchResultContextMenuPropertiesClick(),
                SelectAllHandler = (s, e) => OnSearchResultContextMenuSelectAllClick(),
                //CopyBaseNameHandler = (s, e) => (),
                //CopyFullNameHandler = (s, e) => (),
            };
            return menuHelper.GetContextMenuStrip();
        }

        private void MyFormActivated(object sender, EventArgs eventArgs)
        {
            patternComboBox.Focus();
        }

        private void DirectoryTreeViewOnAfterSelect(object s, TreeViewEventArgs e)
        {
            DirectoryTreeViewActiveAfterSelectNode = e.Node;
            OnDirectoryTreeViewAfterSelect();
        }

        private void DirectoryTreeViewOnBeforeExpand(object s, TreeViewCancelEventArgs e)
        {
            DirectoryTreeViewActiveBeforeExpandNode = e.Node;
            OnDirectoryTreeViewBeforeExpandNode();
        }

        public string Pattern
        {
            get { return patternComboBox.Text; }
            set { patternComboBox.Text = value; }
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
            {   // Directory Tab only holds one root node.
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

        public bool IncludeFiles
        {
            get { return findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 1; }
        }

        public bool IncludeFolders
        {
            get { return findComboBox.SelectedIndex == 0 || findComboBox.SelectedIndex == 2; }
        }

        public int FindEntryFilter
        {
            get { return findComboBox.SelectedIndex; }
            set { findComboBox.SelectedIndex = value; }
        }

        public bool IncludePathInSearch
        {
            get { return whatToSearchComboBox.SelectedIndex == 0; }
            set { whatToSearchComboBox.SelectedIndex = value ? 0 : 1; }
        }

        public void SetSearchResultStatus(int i)
        {
            searchResultsStatus.Text = "SR " + i.ToString();
        }

        public void SetCatalogsLoadedStatus(int i)
        {
            catalogsLoadedStatus.Text = "C " + i.ToString();
        }

        public void SetSearchTimeStatus(string s)
        {
            searchTimeStatus.Text = s;
        }

        public void SetSearchTextBoxAutoComplete(IEnumerable<string> history)
        {
            patternComboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            patternComboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            patternComboBox.AutoCompleteCustomSource = history.ToAutoCompleteStringCollection();
        }

        public void AddSearchTextBoxAutoComplete(string pattern)
        {
            var ac = patternComboBox.AutoCompleteCustomSource;
            if (!ac.Contains(pattern))
            {
                ac.Add(pattern);
            }
        }

        public List<string> GetSearchTextBoxAutoComplete()
        {
            var list = new List<string>(20);
            list.AddRange(patternComboBox.AutoCompleteCustomSource.Cast<string>());
            return list;
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

        public bool SearchButtonEnable
        {
            get { return searchButton.Enabled; }
            set { searchButton.Enabled = value; }
        }

        public string SearchButtonText
        {
            get { return searchButton.Text; }
            set { searchButton.Text = value; }
        }

        public bool IsAdvancedSearchMode
        {
            get { return advancedSearchCheckBox.Checked; }
            set
            {
                advancedSearchCheckBox.Checked = value;
                searchControlAdvancedPanel.Visible = value;
                searchControlAdvancedPanel.Enabled = value;
            }
        }

        public void SetColumnSortCompare<T>(ListViewHelper<T> lvh, Comparison<T> compare) where T : class
        {
            lvh.ColumnSortCompare = compare;
        }

        public int SetList<T>(ListViewHelper<T> lvh, List<T> list) where T : class
        {
            return lvh.SetList(list);
        }

        public void SortList<T>(ListViewHelper<T> lvh) where T : class
        {
            lvh.SortList();
        }

        public DateTime FromDateValue
        {
            get { return fromDateTimePicker.Value; }
            set { fromDateTimePicker.Value = value; }
        }

        public DateTime ToDateValue
        {
            get { return toDateTimePicker.Value; }
            set { toDateTimePicker.Value = value; }
        }

        public DateTime FromHourValue
        {
            get { return fromHourTimePicker.Value; }
            set { fromHourTimePicker.Value = value; }
        }

        public DateTime ToHourValue
        {
            get { return toHourTimePicker.Value; }
            set { toHourTimePicker.Value = value; }
        }
    }
}
