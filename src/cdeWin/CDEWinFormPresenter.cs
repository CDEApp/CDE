using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using cdeLib;
using cdeLib.Infrastructure;

namespace cdeWin
{
    public interface ICDEWinFormPresenter : IPresenter
    {
    }

    public class CDEWinFormPresenter : Presenter<ICDEWinForm>, ICDEWinFormPresenter
    {
        private const string DummyNodeName = "_dummyNode";
        private readonly Color _listViewForeColor = Color.Black;
        private readonly Color _listViewDirForeColor = Color.DarkBlue;

        private readonly ICDEWinForm _clientForm;
        private readonly List<RootEntry> _rootEntries;
        private readonly IConfig _config;

        private readonly string[] _directoryVals;
        private readonly string[] _searchVals;
        private readonly string[] _catalogVals;

        private List<PairDirEntry> _searchResultList;
        private List<DirEntry> _directoryList;
        /// <summary>
        /// The entry that is the parent of the Directory List View displayed items.
        /// </summary>
        private CommonEntry _directoryListCommonEntry;

        private BackgroundWorker _bgWorker;
        private bool _isSearchButton;

        public CDEWinFormPresenter(ICDEWinForm form, List<RootEntry> rootEntries, IConfig config) : base(form)
        {
            _clientForm = form;
            _rootEntries = rootEntries;
            _config = config;

            _searchVals = new string[_config.DefaultSearchResultColumnCount];
            _directoryVals = new string[_config.DefaultDirectoryColumnCount];
            _catalogVals = new string[_config.DefaultCatalogColumnCount];

            SetSearchButton(true);
            RegisterListViewSorters();
            SetCatalogListView();
        }

        private void RegisterListViewSorters()
        {
            _clientForm.SetColumnSortCompare(_clientForm.SearchResultListViewHelper, SearchResultCompare);
            _clientForm.SetColumnSortCompare(_clientForm.DirectoryListViewHelper, DirectoryCompare);
            _clientForm.SetColumnSortCompare(_clientForm.CatalogListViewHelper, RootCompare);
        }

        private void SetCatalogListView()
        {
            var catalogHelper = _clientForm.CatalogListViewHelper;
            var count = _clientForm.SetList(catalogHelper, _rootEntries);
            _clientForm.SortList(catalogHelper);
            _clientForm.SetCatalogsLoadedStatus(count);
        }

        private void SetSearchButton(bool search)
        {
            _isSearchButton = search;
            _clientForm.SearchButtonText = _isSearchButton ? "Search" : "Cancel Search";
        }

        public void Display()
        {
            try
            {
                _clientForm.ShowDialog();
            }
            finally
            {
                _clientForm.Dispose();
            }
        }

        public void DirectoryTreeViewBeforeExpandNode()
        {
            CreateNodesPreExpand(_clientForm.DirectoryTreeViewActiveBeforeExpandNode);
        }

        private static void CreateNodesPreExpand(TreeNode parentNode)
        {
            if (HasDummyChildNode(parentNode))
            {
                // Replace Dummy with real nodes now visible.
                parentNode.Nodes.Clear();
                AddAllDirectoriesChildren(parentNode, (CommonEntry) parentNode.Tag);
            }
        }

        private static bool HasDummyChildNode(TreeNode parentNode)
        {
            return parentNode.Nodes.Count == 1 && parentNode.Nodes[0].Text == DummyNodeName;
        }

        private static void AddAllDirectoriesChildren(TreeNode treeNode, CommonEntry dirEntry)
        {
            foreach (var subDirEntry in dirEntry.Children)
            {
                AddDirectoryChildren(treeNode, subDirEntry);
            }
        }

        private static void AddDirectoryChildren(TreeNode treeNode, DirEntry dirEntry)
        {
            if (dirEntry.IsDirectory)
            {
                var newTreeNode = NewTreeNode(dirEntry, dirEntry.Path);
                treeNode.Nodes.Add(newTreeNode);
                SetDummyChildNode(newTreeNode, dirEntry);
            }
        }

        /// <summary>
        /// A node with children gets a dummy child node so Treeview shows node as expandable.
        /// </summary>
        private static void SetDummyChildNode(TreeNode treeNode, CommonEntry commonEntry)
        {
            if (commonEntry.Children !=null 
                && commonEntry.Children.Any(entry => entry.IsDirectory))
            {
                treeNode.Nodes.Add(NewTreeNode(null, DummyNodeName));
            }
        }

        private static TreeNode NewTreeNode(object tag, string name)
        {
            return new TreeNode(name) {
                //ImageIndex = 0, 
                Tag = tag
            };
        }

        public void CatalogRetrieveVirtualItem()
        {
            var catalogHelper = _clientForm.CatalogListViewHelper;
            if (_rootEntries == null || _rootEntries.Count == 0)
            {
                return;
            }
            var rootEntry = _rootEntries[catalogHelper.RetrieveItemIndex];
            var itemColor = CreateRowValuesForRootEntry(_catalogVals, rootEntry, _listViewForeColor);
            var lvi = BuildListViewItem(_catalogVals, itemColor, rootEntry);
            catalogHelper.RenderItem = lvi;
        }

        private Color CreateRowValuesForRootEntry(string[] vals, RootEntry rootEntry, Color listViewForeColor)
        {
            vals[0] = rootEntry.Path;
            vals[1] = rootEntry.VolumeName;
            vals[2] = rootEntry.DirCount.ToString();
            vals[3] = rootEntry.FileCount.ToString();
            vals[4] = (rootEntry.DirCount + rootEntry.FileCount).ToString();
            vals[5] = rootEntry.DriveLetterHint;
            vals[6] = rootEntry.AvailSpace.ToHRString();
            vals[7] = rootEntry.UsedSpace.ToHRString();
            vals[8] = string.Format(Config.DateFormatYMDHMS, rootEntry.ScanStartUTC.ToLocalTime());
            vals[9] = rootEntry.DefaultFileName; // todo actual file name ? not field stored default.
            vals[10] = rootEntry.SourcePath;
            vals[11] = rootEntry.Description;

            return listViewForeColor;
        }

        public class BgWorkerParam
        {
            public FindOptions Options;
            public List<RootEntry> RootEntries;
            public BgWorkerState State;
        }

        public class BgWorkerState
        {
            public int ListCount;
            public List<PairDirEntry> List;
            public int Counter;
            public int End;
        }

        public void Search()
        {
            if (!_isSearchButton)
            {
                CancelSearch();
                SetSearchButton(true);
                return;
            }

            if (RegexIsBad() 
                || FromToSizeInvalid()
                || FromToDateInvalid()
                || FromToHourInvalid())
            {
                return;
            }
            _clientForm.AddSearchTextBoxAutoComplete(_clientForm.Pattern);

            var optimisedPattern = OptimiseRegexPattern(_clientForm.Pattern);

            var findOptions = new FindOptions
                {
                    LimitResultCount = _clientForm.LimitResultHelper.SelectedValue,
                    Pattern = optimisedPattern,
                    RegexMode = _clientForm.RegexMode,
                    IncludePath = _clientForm.IncludePathInSearch,
                    IncludeFiles = _clientForm.IncludeFiles,
                    IncludeFolders = _clientForm.IncludeFolders,
                    // This many file system entries before progress
                    // for slow regex like example .*moooxxxx.* - 5000 is fairly long on i7.
                    ProgressModifier = 5000, 
                    Worker = _bgWorker,
                    FromSizeEnable = _clientForm.FromSize.Checked,
                    FromSize = FromSizeValue(),
                    ToSizeEnable = _clientForm.ToSize.Checked,
                    ToSize = ToSizeValue(),
                    FromDateEnable = _clientForm.FromDate.Checked,
                    FromDate = _clientForm.FromDateValue.Date,
                    ToDateEnable = _clientForm.ToDate.Checked,
                    ToDate = _clientForm.ToDateValue.Date,
                    FromHourEnable = _clientForm.FromHour.Checked,
                    FromHour = _clientForm.FromHourValue.TimeOfDay,
                    ToHourEnable = _clientForm.ToHour.Checked,
                    ToHour = _clientForm.ToHourValue.TimeOfDay,
                    NotOlderThanEnable = _clientForm.NotOlderThan.Checked,
                    NotOlderThan = NotOlderThanValue(),
                };

            SetSearchButton(false);

            _bgWorker = new BackgroundWorker();
            _bgWorker.WorkerReportsProgress = true;
            _bgWorker.WorkerSupportsCancellation = true;
            _bgWorker.DoWork += BgWorkerDoWork;
            _bgWorker.RunWorkerCompleted += BgWorkerRunWorkerCompleted;
            _bgWorker.ProgressChanged += BgWorkerProgressChanged;
            var param = new BgWorkerParam
                {
                    Options = findOptions,
                    RootEntries = _rootEntries,
                    State = new BgWorkerState()
                };
            _bgWorker.RunWorkerAsync(param);
        }

        private bool FromToDateInvalid()
        {
            if (_clientForm.FromDate.Checked
                && _clientForm.ToDate.Checked
                && _clientForm.FromDateValue.Date >= _clientForm.ToDateValue.Date)
            {
                _clientForm.MessageBox("The From Date Field is greater than the To Date field no search results possible.");
                return true;
            }
            return false;
        }

        private bool FromToHourInvalid()
        {
            if (_clientForm.FromHour.Checked
                && _clientForm.ToHour.Checked
                && _clientForm.FromHourValue.TimeOfDay.TotalSeconds >= _clientForm.ToHourValue.TimeOfDay.TotalSeconds)
            {
                _clientForm.MessageBox("The From Hour Field is greater than the To Hour field no search results possible.");
                return true;
            }
            return false;
        }

        private bool RegexIsBad()
        {
            if (_clientForm.RegexMode)
            {
                var regexError = RegexHelper.GetRegexErrorMessage(_clientForm.Pattern);
                if (!string.IsNullOrEmpty(regexError))
                {
                    _clientForm.MessageBox(regexError);
                    return true;
                }
            }
            return false;
        }

        private bool FromToSizeInvalid()
        {
            if (_clientForm.FromSize.Checked
                && _clientForm.ToSize.Checked
                && FromSizeValue() > ToSizeValue())
            {
                _clientForm.MessageBox("The From Size Field is greater than the To Size field no search results possible.");
                return true;
            }
            return false;
        }

        public long FromSizeValue()
        {
            var value = _clientForm.FromSizeDropDownHelper.SelectedValue;
            return (long)(_clientForm.FromSizeValue.Field * value);
        }

        public long ToSizeValue()
        {
            var value = _clientForm.ToSizeDropDownHelper.SelectedValue;
            return (long)(_clientForm.ToSizeValue.Field * value);
        }

        public DateTime NotOlderThanValue()
        {
            var dropDownValueFunc = _clientForm.NotOlderThanDropDownHelper.SelectedValue;
            var fieldValue = (int)_clientForm.NotOlderThanValue.Field;
            var now = DateTime.Now; // Option to set this to date of newest scanned Catalog
            return dropDownValueFunc(now, -fieldValue); // subtract as we are going back in time.
        }

        // Assumes well formed regex pattern input.
        // As search is substring match remove leading and trailing wildcards.
        protected string OptimiseRegexPattern(string pattern)
        {
            if (_clientForm.RegexMode && pattern != null)
            {
                bool changeMade;
                do
                {
                    changeMade = false;
                    if (pattern.StartsWith(".*"))
                    {
                        pattern = pattern.Substring(2);
                        changeMade = true;
                    }
                    if (pattern.EndsWith(".*"))
                    {
                        pattern = pattern.Substring(0,pattern.Length - 2);
                        changeMade = true;
                    }
                } while (changeMade);
            }
            return pattern;
        }

        private void BgWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            var argument = (BgWorkerParam)e.Argument;
            var findOptions = argument.Options;
            var rootEntries = argument.RootEntries;
            var state = argument.State;

            var list = new List<PairDirEntry>(500);
            state.ListCount = list.Count;   // 0
            state.List = list;
            worker.ReportProgress(0, state);
            findOptions.FoundFunc = (p, d) =>
                {
                    //Thread.Sleep(20);
                    list.Add(new PairDirEntry(p, d));
                    return true;
                };
            findOptions.ProgressFunc = (counter, end) =>
                {
                    state.ListCount = list.Count;   // concurrency !
                    state.List = list;   // concurrency !!!!
                    state.Counter = counter;
                    state.End = end;
                    worker.ReportProgress((int)(100.0 * counter / end), state);
                };
            findOptions.Find(rootEntries);
            state.ListCount = list.Count;
            state.List = list;
            var completePercent = (int) (100.0*state.Counter/state.End);
            if (state.End - state.Counter < findOptions.ProgressModifier)
            {
                completePercent = 100;
            }
            worker.ReportProgress(completePercent, state);
            //worker.ReportProgress(100, state);
            e.Result = list;
        }

        private void BgWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var searchHelper = _clientForm.SearchResultListViewHelper;
            int count;

            if (e.Error != null)
            {
                _clientForm.MessageBox(e.Error.Message);
                count = 0;
            }
            else if (e.Cancelled)
            {
                count = searchHelper.SetList(_searchResultList);
            }
            else
            {
                var resultList = (List<PairDirEntry>)e.Result;
                _searchResultList = resultList;
                count = searchHelper.SetList(_searchResultList);
            }
            _clientForm.SetSearchResultStatus(count);
            _clientForm.SortList(searchHelper);
            SetSearchButton(true);
            _bgWorker.Dispose();
            _bgWorker = null;
        }

        private void BgWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var state = (BgWorkerState)e.UserState;
            var p = e.ProgressPercentage;
            _clientForm.SetSearchTimeStatus("% " + p 
                + " lc " + state.ListCount
                + " ctr " + state.Counter
                + " end " + state.End
                );

            // think about only sorting when done ???? so a set without sort...
            _searchResultList = state.List; // snag so is avail for Completed
            var searchHelper = _clientForm.SearchResultListViewHelper;
            var count = searchHelper.SetList(_searchResultList);
            _clientForm.SetSearchResultStatus(count);
        }

        public void CancelSearch()
        {
            if (_bgWorker != null)
            {
                _bgWorker.CancelAsync();
            }
        }

        public void SearchResultRetrieveVirtualItem()
        {
            var searchHelper = _clientForm.SearchResultListViewHelper;
            if (_searchResultList == null || _searchResultList.Count == 0)
            {
                return;
            }
            var pairDirEntry = _searchResultList[searchHelper.RetrieveItemIndex];
            var dirEntry = pairDirEntry.ChildDE;
            var itemColor = CreateRowValuesForDirectory(_searchVals, dirEntry, _listViewForeColor);

            _searchVals[3] = pairDirEntry.ParentDE.FullPath;
            var lvi = BuildListViewItem(_searchVals, itemColor, pairDirEntry);
            searchHelper.RenderItem = lvi;
        }

        public void DirectoryTreeViewAfterSelect()
        {
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            var selectedNode = _clientForm.DirectoryTreeViewActiveAfterSelectNode;
            var commonEntry = (CommonEntry)selectedNode.Tag;
            // _directoryList = null; // removed for now - maybe caused Null Exception TODO think ? though if refer wrong data then problem ?
            _directoryListCommonEntry = null;
            if (commonEntry != null)
            {
                _directoryListCommonEntry = commonEntry;
                _directoryList = commonEntry.Children != null ? commonEntry.Children.ToList() : null;
                directoryHelper.SetList(_directoryList);
                _clientForm.SetDirectoryPathTextbox = commonEntry.FullPath;
            }
        }

        public void DirectoryRetrieveVirtualItem()
        {
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            if (_directoryList == null || _directoryList.Count == 0)
            { 
                return;
            }
            var dirEntry = _directoryList[directoryHelper.RetrieveItemIndex];
            var itemColor = CreateRowValuesForDirectory(_directoryVals, dirEntry, _listViewForeColor);
            var lvi = BuildListViewItem(_directoryVals, itemColor, dirEntry);
            directoryHelper.RenderItem = lvi;
        }


        private Color CreateRowValuesForDirectory(IList<string> vals, DirEntry dirEntry, Color itemColor)
        {
            vals[0] = dirEntry.Path;
            vals[1] = dirEntry.Size.ToString();
            if (dirEntry.IsDirectory)
            {
                itemColor = _listViewDirForeColor;
                if (dirEntry.IsDirectory)
                {
                    var val = dirEntry.Size.ToHRString()
                        + " <Dir";
                    if (dirEntry.IsReparsePoint)
                    {
                        val += " R";
                    }
                    if (dirEntry.IsSymbolicLink)
                    {
                        val += " S";
                    }
                    vals[1] = val + ">";
                }
            }
            vals[2] = dirEntry.IsModifiedBad ? "<Bad Date>" : string.Format(Config.DateFormatYMDHMS, dirEntry.Modified);
            return itemColor;
        }

        // before form closes capture any changed configuration.
        public void MyFormClosing()
        {
            _config.RecordConfig(_clientForm);
            _clientForm.CleanUp();
        }

        public void CatalogListViewItemActivate()
        {
            _clientForm.CatalogListViewHelper.ActionOnActivateItem(GoToDirectoryRoot);
        }
        
        public void GoToDirectoryRoot(RootEntry newRoot)
        {
            RootEntry currentRoot = null;
            var directoryTreeViewNodes = _clientForm.DirectoryTreeViewNodes;
            if (directoryTreeViewNodes != null)
            {
                currentRoot = (RootEntry)directoryTreeViewNodes.Tag;
            }

            if (currentRoot == null || currentRoot != newRoot)
            {
                SetNewDirectoryRoot(newRoot);
            }
            _clientForm.SelectDirectoryPane();
        }

        private TreeNode SetNewDirectoryRoot(RootEntry newRoot)
        {
            var newRootNode = BuildRootNode(newRoot);
            _clientForm.DirectoryTreeViewNodes = newRootNode;
            _clientForm.DirectoryListViewHelper.InitSort();
            return newRootNode;
        }

        private static TreeNode BuildRootNode(RootEntry rootEntry)
        {
            var rootTreeNode = NewTreeNode(rootEntry, rootEntry.Path);
            SetDummyChildNode(rootTreeNode, rootEntry);
            return rootTreeNode;
        }

        public void DirectoryListViewItemActivate()
        {
            _clientForm.DirectoryListViewHelper.ActionOnActivateItem(d => 
                {
                    if (d.IsDirectory)
                    {
                        SetDirectoryWithExpand(d);
                    }
                });
        }

        private void SetDirectoryWithExpand(DirEntry dirEntry)
        {
            var activatedDirEntryList = dirEntry.GetListFromRoot();

            SetDirectoryWithExpand(activatedDirEntryList);
        }

        private void SetDirectoryWithExpand(IEnumerable<CommonEntry> activatedDirEntryList)
        {
            var currentRootNode = _clientForm.DirectoryTreeViewNodes;
            RootEntry currentRoot = null;
            if (currentRootNode != null)
            {
                currentRoot = (RootEntry) currentRootNode.Tag;
            }
            TreeNode workingTreeNode = null;
            RootEntry newRoot = null;
            foreach (var entry in activatedDirEntryList)
            {
                if (newRoot == null)
                {
                    newRoot = (RootEntry) entry;
                    if (currentRoot != newRoot)
                    {
                        currentRootNode = SetNewDirectoryRoot(newRoot);
                        currentRoot = newRoot;
                    }
                    workingTreeNode = currentRootNode; // starting at rootnode.
                }
                else
                {
                    if (((DirEntry) entry).IsDirectory && workingTreeNode != null)
                    {
                        CreateNodesPreExpand(workingTreeNode);
                        workingTreeNode.Expand();
                        object findTag = entry;
                        var nodeForCurrentEntry = workingTreeNode.Nodes.Cast<TreeNode>()
                            .FirstOrDefault(node => node.Tag == findTag);
                        workingTreeNode = nodeForCurrentEntry;
                    }
                }
            }

            if (workingTreeNode != null)
            {
                CreateNodesPreExpand(workingTreeNode);
                workingTreeNode.Expand();
                _clientForm.DirectoryTreeViewSelectedNode = workingTreeNode;

                // This is required or item under cursor after double click is selected.
                // not sure why ? some sort of left over click on new ListView content.
                var directoryHelper = _clientForm.DirectoryListViewHelper;
                directoryHelper.DeselectItems();
                //_clientForm.DirectoryListViewDeselectItems();

                _clientForm.SelectDirectoryPane();
            }
        }

        public void SearchResultListViewItemActivate()
        {
            _clientForm.SearchResultListViewHelper.ActionOnActivateItem(ViewFileInDirectoryTab);
        }

        public void DirectoryListViewItemSelectionChanged()
        {
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            var indices = directoryHelper.SelectedIndices;
            var indicesCount = directoryHelper.SelectedIndicesCount;
            if (indicesCount > 0)
            {
                var firstIndex = indices.First();
                var dirEntry = _directoryList[firstIndex]; // got a Null on _directoryList TODO ? fix ?
                _clientForm.SetDirectoryPathTextbox = indicesCount > 1 
                    ? _directoryListCommonEntry.FullPath 
                    : _directoryListCommonEntry.MakeFullPath(dirEntry);
            }
        }

        public void SearchResultListViewColumnClick()
        {
            _clientForm.SearchResultListViewHelper.ListViewColumnClick();
        }

        private int SearchResultCompare(PairDirEntry pde1, PairDirEntry pde2)
        {
            int compareResult;
            var de1 = pde1.ChildDE;
            var de2 = pde2.ChildDE;
            var searchResultHelper = _clientForm.SearchResultListViewHelper;
            var sortColumn = searchResultHelper.SortColumn;
            switch (sortColumn)
            {
                case 0: // SearchResult ListView Name column
                    compareResult = de1.PathCompareWithDirTo(de2);
                    break;

                case 1: // SearchResult ListView Size column
                    compareResult = de1.SizeCompareWithDirTo(de2);
                    break;

                case 2: // SearchResult ListView Modified column
                    compareResult = de1.ModifiedCompareTo(de2);
                    break;

                case 3: // SearchResult ListView Path column
                    //var compareResult = _myCompareInfo.Compare(pde1.FullPath, pde2.FullPath, MyCompareOptions);
                    compareResult = Config.MyCompareInfo.Compare(pde1.ParentDE.FullPath, pde2.ParentDE.FullPath, Config.MyCompareOptions);
                    if (compareResult == 0)
                    {
                        compareResult = Config.MyCompareInfo.Compare(de1.Path, de2.Path, Config.MyCompareOptions);
                    }
                    break;

                default:
                    throw new Exception(string.Format("Problem column {0} not handled for sort.", sortColumn));
            }
            if (searchResultHelper.ColumnSortOrder == SortOrder.Descending)
            {
                compareResult *= -1;
            }
            return compareResult;
        }

        public void DirectoryListViewColumnClick()
        {
            _clientForm.DirectoryListViewHelper.ListViewColumnClick();
        }

        private int DirectoryCompare(DirEntry de1, DirEntry de2)
        {
            int compareResult;
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            var column = directoryHelper.SortColumn;
            switch (column)
            {
                case 0: // SearchResult ListView Name column
                    compareResult = de1.PathCompareWithDirTo(de2);
                    break;

                case 1: // SearchResult ListView Size column
                    compareResult = de1.SizeCompareWithDirTo(de2);
                    break;

                case 2: // SearchResult ListView Modified column
                    compareResult = de1.ModifiedCompareTo(de2);
                    break;

                default:
                    throw new Exception(string.Format("Problem column {0} not handled for sort.", column));
            }
            if (directoryHelper.ColumnSortOrder == SortOrder.Descending)
            {
                compareResult *= -1;
            }
            return compareResult;
        }

        public void ExitMenuItem()
        {
            _clientForm.Close();
        }

        private void DirectoryGetContextMenuPairDirEntryThatExists(Action<PairDirEntry> gotContextAction)
        {
            _clientForm.DirectoryListViewHelper.ActionOnSelectedItem(d =>
                {
                    var pde = new PairDirEntry(_directoryListCommonEntry, d);
                    if (pde.ExistsOnFileSystem())
                    {
                        gotContextAction(pde);
                    }
                });
        }

        public void DirectoryContextMenuViewTreeClick()
        {
            DirectoryGetContextMenuPairDirEntryThatExists(ViewFolderInDirectoryTab);
        }

        private void ViewFolderInDirectoryTab(PairDirEntry pde)
        {
            var dirEntry = pde.ChildDE;
            if (dirEntry.IsDirectory)
            {
                SetDirectoryWithExpand(dirEntry);
            }
        }

        private void ViewFileInDirectoryTab(PairDirEntry pde)
        {
            var dirEntry = pde.ChildDE;
            SetDirectoryWithExpand(dirEntry);

            SelectFileInDirectoryTab(dirEntry);
        }

        private void SelectFileInDirectoryTab(DirEntry dirEntry)
        {
            if (!dirEntry.IsDirectory)
            {
                var index = _directoryList.IndexOf(dirEntry);
                var directoryHelper = _clientForm.DirectoryListViewHelper;
                directoryHelper.SelectItem(index);
            }
        }

        public void DirectoryContextMenuOpenClick()
        {
            DirectoryGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ExplorerOpen(pde.FullPath));
        }

        public void DirectoryContextMenuExploreClick()
        {
            DirectoryGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ExplorerExplore(pde.FullPath));
        }

        public void DirectoryContextMenuPropertiesClick()
        {
            DirectoryGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ShowFileProperties(pde.FullPath));
        }

        public void DirectoryContextMenuSelectAllClick()
        {
            _clientForm.DirectoryListViewHelper.SelectAllItems();
        }

        public void DirectoryContextMenuParentClick()
        {
            var entryList = _directoryListCommonEntry.GetListFromRoot();
            if (entryList.Count > 1)
            {
                entryList.RemoveAt(entryList.Count-1);
                SetDirectoryWithExpand(entryList);
            }
        }

        private void SearchResultGetContextMenuPairDirEntryThatExists(Action<PairDirEntry> gotContextAction)
        {
            _clientForm.SearchResultListViewHelper.ActionOnSelectedItem(pde =>
                {
                    if (pde.ExistsOnFileSystem())
                    {
                        gotContextAction(pde);
                    }
                });
        }

        public void SearchResultContextMenuViewTreeClick()
        {
            SearchResultGetContextMenuPairDirEntryThatExists(ViewFileInDirectoryTab);
        }

        public void SearchResultContextMenuOpenClick()
        {
            SearchResultGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ExplorerOpen(pde.FullPath));
        }

        public void SearchResultContextMenuExploreClick()
        {
            SearchResultGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ExplorerExplore(pde.FullPath));
        }

        public void SearchResultContextMenuPropertiesClick()
        {
            SearchResultGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ShowFileProperties(pde.FullPath));
        }

        public void SearchResultContextMenuSelectAllClick()
        {
            _clientForm.SearchResultListViewHelper.SelectAllItems();
        }

        public ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag)
        {
            var lvItem = new ListViewItem(vals[0]); 
            // a bug this doesnt work under mouse cursor { UseItemStyleForSubItems = false };
            // lvItem.SubItems[0].ForeColor = firstColumnForeColor;
            lvItem.ForeColor = firstColumnForeColor;
            lvItem.Tag = tag;
            for (var i = 1; i < vals.Length; ++i)
            {
                lvItem.SubItems.Add(vals[i]);
                //lvItem.SubItems[i].ForeColor = _listViewForeColor; // set others to other than item
            }
            return lvItem;
        }

        public void CatalogListViewColumnClick()
        {
            _clientForm.CatalogListViewHelper.ListViewColumnClick();
        }

        private int RootCompare(RootEntry re1, RootEntry re2)
        {
            int compareResult;
            var catalogHelper = _clientForm.CatalogListViewHelper;
            var column = catalogHelper.SortColumn;
            switch (column)
            {
                case 0:
                    compareResult = re1.Path.CompareTo(re2.Path);
                    break;

                case 1:
                    compareResult = re1.VolumeName.CompareTo(re2.VolumeName);
                    break;

                case 2:
                    compareResult = re1.DirCount.CompareTo(re2.DirCount);
                    break;

                case 3:
                    compareResult = re1.FileCount.CompareTo(re2.FileCount);
                    break;

                case 4:
                    compareResult = (re1.DirCount+ re1.FileCount).CompareTo(re2.DirCount + re2.FileCount);
                    break;

                case 5:
                    compareResult = re1.DriveLetterHint.CompareTo(re2.DriveLetterHint);
                    break;

                case 6:
                    compareResult = re1.AvailSpace.CompareTo(re2.AvailSpace);
                    break;

                case 7:
                    compareResult = re1.UsedSpace.CompareTo(re2.UsedSpace);
                    break;

                case 8:
                    compareResult = re1.ScanStartUTC.CompareTo(re2.ScanStartUTC);
                    break;

                case 9:
                    compareResult = re1.DefaultFileName.CompareTo(re2.DefaultFileName);
                    break;

                case 10:
                    compareResult = re1.SourcePath.CompareTo(re2.SourcePath);
                    break;

                case 11:
                    compareResult = re1.DescriptionCompareTo(re2);
                    break;

                default:
                    throw new Exception(string.Format("Problem column {0} not handled for sort.", column));
            }
            if (catalogHelper.ColumnSortOrder == SortOrder.Descending)
            {
                compareResult *= -1;
            }
            return compareResult;
        }

        public void AdvancedSearchCheckboxChanged()
        {
            var isAdvanced = _clientForm.IsAdvancedSearchMode;
            SetAdvancedSearch(isAdvanced);
        }

        private void SetAdvancedSearch(bool value)
        {
            _clientForm.IsAdvancedSearchMode = value;
        }
    }
}
