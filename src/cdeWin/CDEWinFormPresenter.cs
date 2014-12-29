using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Util;
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
        protected List<RootEntry> _rootEntries;
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
        private readonly ILoadCatalogService _loadCatalogService;

        public CDEWinFormPresenter(ICDEWinForm form, IConfig config, ILoadCatalogService loadCatalogService = null) : base(form)
        {
            var timeIt = new TimeIt();
            _clientForm = form;
            _config = config;
            _loadCatalogService = loadCatalogService;
            _rootEntries = LoadRootEntries(config, timeIt);

            _searchVals = new string[_config.DefaultSearchResultColumnCount];
            _directoryVals = new string[_config.DefaultDirectoryColumnCount];
            _catalogVals = new string[_config.DefaultCatalogColumnCount];

            SetSearchButton(true);
            RegisterListViewSorters();
            SetCatalogListView();

			InitialiseLog(timeIt);
        }

        protected List<RootEntry> LoadRootEntries(IConfig config, TimeIt timeIt)
        {
            if (_loadCatalogService != null)
            {
                return _loadCatalogService.LoadRootEntries(config, timeIt);
            }
            return null;
        }

        private void InitialiseLog(TimeIt timeIt)
        {
            _clientForm.Addline("{0} v{1}", _config.ProductName, _config.Version);
            LogTimeIt(timeIt);
        }

        private void LogTimeIt(TimeIt timeIt)
        {
            if (timeIt != null)
            {
                foreach (var labelElapsed in timeIt.ElapsedList)
                {
                    _clientForm.Addline("Loaded {0} in {1} msec", labelElapsed.Label, labelElapsed.ElapsedMsec);
                }
                _clientForm.Addline("Total Load time for {0} files in {1} msec", timeIt.ElapsedList.Count(), timeIt.TotalMsec);
            }
        }

        private void RegisterListViewSorters()
        {
            _clientForm.SearchResultListViewHelper.ColumnSortCompare =  SearchResultCompare;
            _clientForm.DirectoryListViewHelper.ColumnSortCompare = DirectoryCompare;
            _clientForm.CatalogListViewHelper.ColumnSortCompare = RootCompare;
        }

        private void SetCatalogListView()
        {
            var catalogHelper = _clientForm.CatalogListViewHelper;
            var count = catalogHelper.SetList(_rootEntries);
            catalogHelper.SortList();
            _clientForm.SetCatalogsLoadedStatus(count);
            _clientForm.SetTotalFileEntriesLoadedStatus(_rootEntries.TotalFileEntries());
        }

        private void SetSearchButton(bool search)
        {
            _isSearchButton = search;
            _clientForm.SearchButtonText = _isSearchButton ? "Search" : "Cancel Search";
            _clientForm.SearchButtonBackColor = _isSearchButton ? default(Color) : Color.LightCoral;
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

        public void FormActivated()
        {
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
                var newTreeNode = NewTreeNode(dirEntry);
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
                treeNode.Nodes.Add(NewTreeNode(DummyNodeName));
            }
        }

        private static TreeNode NewTreeNode(CommonEntry commonEntry)
        {
            return NewTreeNode(commonEntry.Path, commonEntry);
        }

        private static TreeNode NewTreeNode(string name, object tag = null)
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
            vals[2] = rootEntry.DirEntryCount.ToString();
            vals[3] = rootEntry.FileEntryCount.ToString();
            vals[4] = (rootEntry.DirEntryCount + rootEntry.FileEntryCount).ToString();
            vals[5] = rootEntry.DriveLetterHint;
            vals[6] = rootEntry.Size.ToHRString();
            vals[7] = rootEntry.AvailSpace.ToHRString();
            vals[8] = rootEntry.TotalSpace.ToHRString();
            vals[9] = string.Format(_config.DateFormatYMDHMS, rootEntry.ScanStartUTC.ToLocalTime());
            vals[10] = string.Format("{0:0.} msec", rootEntry.ScanDurationMilliseconds);
            vals[11] = rootEntry.ActualFileName;
            vals[12] = rootEntry.Description;

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

            SetSearchButton(false);

            _bgWorker = new BackgroundWorker();
            _bgWorker.WorkerReportsProgress = true;
            _bgWorker.WorkerSupportsCancellation = true;
            _bgWorker.DoWork += BgWorkerDoWork;
            _bgWorker.RunWorkerCompleted += BgWorkerRunWorkerCompleted;
            _bgWorker.ProgressChanged += BgWorkerProgressChanged;

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
                    ProgressModifier = 50000, 
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

        private long FromSizeValue()
        {
            var value = _clientForm.FromSizeDropDownHelper.SelectedValue;
            return (long)(_clientForm.FromSizeValue.Field * value);
        }

        private long ToSizeValue()
        {
            var value = _clientForm.ToSizeDropDownHelper.SelectedValue;
            return (long)(_clientForm.ToSizeValue.Field * value);
        }

        private DateTime NotOlderThanValue()
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
            findOptions.VisitorFunc = (p, d) =>
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

            if (e == null)
            {
                count = 0;
            }
            else
            {
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
                    count = SetSearchResultList(resultList);
                }
            }
            _clientForm.SetSearchResultStatus(count);
            searchHelper.SortList();
            SetSearchButton(true);
            _bgWorker.Dispose();
            _bgWorker = null;
        }

        private void BgWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var state = (BgWorkerState)e.UserState;
            var p = e.ProgressPercentage;
            _clientForm.SetSearchTimeStatus("% " + p 
                //+ " lc " + state.ListCount
                //+ " ctr " + state.Counter
                //+ " end " + state.End
                );

            var count = SetSearchResultList(state.List);
            _clientForm.SetSearchResultStatus(count);
        }

        protected int SetSearchResultList(List<PairDirEntry> list)
        {
            _searchResultList = list;
            return _clientForm.SearchResultListViewHelper.SetList(list);
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
            var selectedNode = _clientForm.DirectoryTreeViewActiveAfterSelectNode;
            SetDirectoryListView((CommonEntry)selectedNode.Tag);
        }

        public void SetDirectoryListView(CommonEntry commonEntry)
        {
            _directoryListCommonEntry = commonEntry;
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            _directoryList = commonEntry.Children != null ? commonEntry.Children.ToList() : null;
            directoryHelper.SetList(_directoryList);
            directoryHelper.SortList();
            _clientForm.SetDirectoryPathTextbox = commonEntry.FullPath;
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
			vals[2] = dirEntry.IsModifiedBad ? "<Bad Date>" : string.Format(_config.DateFormatYMDHMS, dirEntry.Modified);
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
            var rootTreeNode = NewTreeNode(rootEntry);
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
                directoryHelper.DeselectAllItems();
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
                var dirEntry = _directoryList[firstIndex];
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
                    compareResult = _config.MyCompareInfo.Compare(pde1.ParentDE.FullPath, pde2.ParentDE.FullPath, _config.MyCompareOptions);
                    if (compareResult == 0)
                    {
						compareResult = _config.MyCompareInfo.Compare(de1.Path, de2.Path, _config.MyCompareOptions);
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

        public void AboutMenuItem()
        {
            _clientForm.AboutDialog();
        }

        private void DirectoryTreeGetContextMenuPairDirEntryThatExists(Action<CommonEntry> gotContextAction)
        {
            var selectedCommonEntry = _clientForm.GetSelectedTreeItem();
            if (selectedCommonEntry.ExistsOnFileSystem())
            {
                gotContextAction(selectedCommonEntry);
            }
        }

        public void DirectoryTreeContextMenuOpenClick()
        {
            DirectoryTreeGetContextMenuPairDirEntryThatExists(ce => WindowsExplorerUtilities.ExplorerOpen(ce.FullPath));
        }

        public void DirectoryTreeContextMenuExploreClick()
        {
            DirectoryTreeGetContextMenuPairDirEntryThatExists(ce => WindowsExplorerUtilities.ExplorerExplore(ce.FullPath));
        }

        public void DirectoryTreeContextMenuPropertiesClick()
        {
            DirectoryTreeGetContextMenuPairDirEntryThatExists(ce => WindowsExplorerUtilities.ShowFileProperties(ce.FullPath));
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

        private void DirectoryGetContextMenuPairDirEntrys(Action<IEnumerable<DirEntry>> gotContextAction)
        {
            _clientForm.DirectoryListViewHelper.ActionOnSelectedItems(gotContextAction);
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

        public void DirectoryContextMenuCopyFullPathClick()
        {
            DirectoryGetContextMenuPairDirEntrys(enumerableDirEntry =>
            {
                // we dont have parent dir entry here ... 
                var s = new StringBuilder();
                foreach (var dirEntry in enumerableDirEntry)
                {
                    var pde = new PairDirEntry(_directoryListCommonEntry, dirEntry);
                    s.Append(pde.FullPath + Environment.NewLine);
                }
                Clipboard.SetText(s.ToString());
            });
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

        private void SearchResultGetContextMenuPairDirEntrys(Action<IEnumerable<PairDirEntry>> gotContextAction)
        {
            _clientForm.SearchResultListViewHelper.ActionOnSelectedItems(gotContextAction);
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

        public void SearchResultContextMenuCopyFullPathClick()
        {
            SearchResultGetContextMenuPairDirEntrys(listPDE  =>
                {
                    var s = new StringBuilder();
                    foreach (var pairDirEntry in listPDE)
                    {
                        s.Append(pairDirEntry.FullPath + Environment.NewLine);
                    }
                    Clipboard.SetText(s.ToString());
                });
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
                    compareResult = re1.DirEntryCount.CompareTo(re2.DirEntryCount);
                    break;

                case 3:
                    compareResult = re1.FileEntryCount.CompareTo(re2.FileEntryCount);
                    break;

                case 4:
                    compareResult = (re1.DirEntryCount + re1.FileEntryCount).CompareTo(re2.DirEntryCount + re2.FileEntryCount);
                    break;

                case 5:
                    compareResult = re1.DriveLetterHint.CompareTo(re2.DriveLetterHint);
                    break;

                case 6:
                    compareResult = re1.Size.CompareTo(re2.Size);
                    break;

                case 7:
                    compareResult = re1.AvailSpace.CompareTo(re2.AvailSpace);
                    break;

                case 8:
                    compareResult = re1.TotalSpace.CompareTo(re2.TotalSpace);
                    break;

                case 9:
                    compareResult = re1.ScanStartUTC.CompareTo(re2.ScanStartUTC);
                    break;

                case 10:
                    compareResult = re1.ScanDurationMilliseconds.CompareTo(re2.ScanDurationMilliseconds);
                    break;

                case 11:
                    compareResult = re1.ActualFileName.CompareTo(re2.ActualFileName);
                    break;

                case 12:
                    compareResult = re1.DescriptionCompareTo(re2, _config);
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

        public void ReloadCatalogs()
        {
            // clear all current list views and tree views.
            var catalogHelper = _clientForm.CatalogListViewHelper;
            catalogHelper.SetList(null);
            var searchResultHelper = _clientForm.SearchResultListViewHelper;
            searchResultHelper.SetList(null);
            var directoryListHelper = _clientForm.DirectoryListViewHelper;
            directoryListHelper.SetList(null);

            var previousRootEntries = _rootEntries;
            foreach (var rootEntry in previousRootEntries)
            {
                rootEntry.ClearCommonEntryFields();
            }

            var timeIt = new TimeIt();
            var newRootEntries = LoadRootEntries(_config, timeIt);
            _rootEntries = newRootEntries;
            if (_rootEntries.Count > 1)
            {   // this resets the tree view and directory list view
                SetNewDirectoryRoot(_rootEntries.First());
            }

            // set root entries.
            SetCatalogListView();

            _clientForm.Addline(string.Empty);
            _clientForm.Addline("{0} v{1} reloading catalogs", _config.ProductName, _config.Version);
            LogTimeIt(timeIt);
        }
    }
}
