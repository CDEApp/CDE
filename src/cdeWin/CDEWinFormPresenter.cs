using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
        private const string ModifiedFieldFormat = "{0:yyyy/MM/dd HH:mm:ss}";
        private const string DummyNodeName = "_dummyNode";
        private readonly Color _listViewForeColor = Color.Black;
        private readonly Color _listViewDirForeColor = Color.DarkBlue;

        // TODO these need to be centralised.
        private const CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
        private readonly CompareInfo _myCompareInfo = CompareInfo.GetCompareInfo("en-US");

        private readonly ICDEWinForm _clientForm;
        private readonly List<RootEntry> _rootEntries;
        private readonly Config _config;

        private readonly string[] _directoryVals;
        private readonly string[] _searchVals;
        private readonly string[] _catalogVals;

        private List<PairDirEntry> _searchResultList;
        private List<DirEntry> _directoryList;
        /// <summary>
        /// The entry that is the parent of the Directory List View displayed items.
        /// </summary>
        private CommonEntry _directoryListCommonEntry;


        public CDEWinFormPresenter(ICDEWinForm form, List<RootEntry> rootEntries, Config config) : base(form)
        {
            _clientForm = form;
            _rootEntries = rootEntries;
            _config = config;

            var cfg = _config.Active;
            _directoryVals = new string[cfg.DirectoryListView.Columns.Count];
            _searchVals = new string[cfg.SearchResultListView.Columns.Count];
            _catalogVals = new string[cfg.CatalogListView.Columns.Count];

            RegisterListViewSorters();
            SetCatalogListView();
        }

        private void RegisterListViewSorters()
        {
            _clientForm.SearchResultListViewHelper.ColumnSortCompare = SearchResultCompare;
            _clientForm.DirectoryListViewHelper.ColumnSortCompare = DirectoryCompare;
            _clientForm.CatalogListViewHelper.ColumnSortCompare = RootCompare;
        }

        private void SetCatalogListView()
        {
            var catalogHelper = _clientForm.CatalogListViewHelper;
            var count = catalogHelper.SetList(_rootEntries);
            _clientForm.SetCatalogsLoadedStatus(count);
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

        private static TreeNode BuildRootNode(RootEntry rootEntry)
        {
            var rootTreeNode = NewTreeNode(rootEntry, rootEntry.Path);
            SetDummyChildNode(rootTreeNode, rootEntry);
            return rootTreeNode;
        }

        /// <summary>
        /// A node with children gets a dummy child node to display as an expandable node.
        /// </summary>
        private static void SetDummyChildNode(TreeNode treeNode, CommonEntry commonEntry)
        {
            if (commonEntry.Children !=null 
                && commonEntry.Children.Any(entry => entry.IsDirectory))
            {
                treeNode.Nodes.Add(NewTreeNode(null, DummyNodeName));
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

        private static TreeNode NewTreeNode(object tag, string name)
        {
            return new TreeNode(name) {
                //ImageIndex = 0, 
                Tag = tag
            };
        }

        private Color CreateRowValuesForDirEntry(IList<string> vals, DirEntry dirEntry, Color itemColor)
        {
            vals[0] = dirEntry.Path;
            vals[1] = dirEntry.Size.ToString();
            if (dirEntry.IsDirectory)
            {
                itemColor = _listViewDirForeColor;
                if (dirEntry.IsDirectory)
                {
                    var val = "<Dir";
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
            vals[2] = dirEntry.IsModifiedBad ? "<Bad Date>" : string.Format(ModifiedFieldFormat, dirEntry.Modified);
            return itemColor;
        }

        public void CatalogRetrieveVirtualItem()
        {
            if (_rootEntries == null || _rootEntries.Count == 0)
            {   // in case we get called a bit early.
                return;
            }
            var catalogHelper = _clientForm.CatalogListViewHelper;
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
            vals[8] = string.Format(ModifiedFieldFormat, rootEntry.ScanStartUTC);
            vals[9] = rootEntry.DefaultFileName; // todo give full path ? or actual file name ?
            vals[10] = rootEntry.Description;

            return listViewForeColor;
        }

        public void SearchRoots()
        {
            if (_clientForm.RegexMode)
            {
                var regexError = RegexHelper.GetRegexErrorMessage(_clientForm.Pattern);
                if (!string.IsNullOrEmpty(regexError))
                {
                    MessageBox.Show(regexError);
                    return;
                }
            }
            _clientForm.SearchButtonEnable = false;
            var searchHelper = _clientForm.SearchResultListViewHelper;
            _clientForm.AddSearchTextBoxAutoComplete(_clientForm.Pattern);

            string trace = string.Empty;
            var sw = new Stopwatch();
            //sw.Start();
            //var resultEnum = Find.TraverseTreeFind(_rootEntries, pattern, regexMode, _clientForm.IncludePathInSearch);
            //_searchResultList = resultEnum.ToList();
            //sw.Stop();
            //trace = "ST " + sw.ElapsedMilliseconds;

            var findOptions = new FindOptions
                {
                    Pattern = _clientForm.Pattern,
                    RegexMode = _clientForm.RegexMode,
                    IncludePath = _clientForm.IncludePathInSearch,
                    IncludeFiles = _clientForm.IncludeFiles,
                    IncludeFolders = _clientForm.IncludeFolders,
                    FoundFunc = (p, d) =>
                    {
                        _searchResultList.Add(new PairDirEntry(p, d));
                        return true;
                    },
                };

            sw.Start();
            _searchResultList = new List<PairDirEntry>();
            Find.TraverseTreeFind(_rootEntries, findOptions);
            sw.Stop();
            trace += " ST " + sw.ElapsedMilliseconds;
            _clientForm.SetSearchTimeStatus(trace);

            var count = searchHelper.SetList(_searchResultList);
            _clientForm.SetSearchResultStatus(count);
            _clientForm.SearchButtonEnable = true;
        }

        public void SearchResultRetrieveVirtualItem()
        {
            if (_searchResultList == null || _searchResultList.Count == 0)
            {   // in case we get called a bit early.
                return;
            }
            var searchHelper = _clientForm.SearchResultListViewHelper;
            var pairDirEntry = _searchResultList[searchHelper.RetrieveItemIndex];
            var dirEntry = pairDirEntry.ChildDE;
            var itemColor = CreateRowValuesForDirEntry(_searchVals, dirEntry, _listViewForeColor);

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
            if (_directoryList == null || _directoryList.Count == 0)
            {   // in case we get called a bit early.
                return;
            }
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            var dirEntry = _directoryList[directoryHelper.RetrieveItemIndex];
            var itemColor = CreateRowValuesForDirEntry(_directoryVals, dirEntry, _listViewForeColor);
            var lvi = BuildListViewItem(_directoryVals, itemColor, dirEntry);
            directoryHelper.RenderItem = lvi;
        }

        // before form closes capture any changed configuration.
        public void MyFormClosing()
        {
            _config.RecordConfig(_clientForm);
        }

        public void CatalogListViewItemActivate()
        {
            _clientForm.CatalogListViewHelper.ActionOnActivateItem(GoToDirectoryRoot);
        }
        
        public void GoToDirectoryRoot(RootEntry newRoot)
        {
            RootEntry currentRoot = null;
            if (_clientForm.DirectoryTreeViewNodes != null)
            {
                currentRoot = (RootEntry) _clientForm.DirectoryTreeViewNodes.Tag;
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
                _clientForm.SetDirectoryTreeViewSelectedNode = workingTreeNode;

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
                    compareResult = _myCompareInfo.Compare(pde1.ParentDE.FullPath, pde2.ParentDE.FullPath, MyCompareOptions);
                    if (compareResult == 0)
                    {
                        compareResult = _myCompareInfo.Compare(de1.Path, de2.Path, MyCompareOptions);
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
            _clientForm.DirectoryListViewHelper.ActionOnContextItem(d =>
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
            _clientForm.SearchResultListViewHelper.ActionOnContextItem(pde =>
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
    }
}