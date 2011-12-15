using System;
using System.Collections.Generic;
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
        private readonly Color _listViewDirForeColor = Color.Blue;

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
        private CommonEntry _directoryListCommonEntry;

        private SortOrder _searchResultSortOrder;
        private int _searchResultSortColumn;
        private SortOrder _directorySortOrder;
        private int _directorySortColumn;

        public CDEWinFormPresenter(ICDEWinForm form, List<RootEntry> rootEntries, Config config) : base(form)
        {
            _clientForm = form;
            _rootEntries = rootEntries;
            _config = config;

            var cfg = _config.Active;
            _directoryVals = new string[cfg.DirectoryListView.Columns.Count];
            _searchVals = new string[cfg.SearchResultListView.Columns.Count];
            _catalogVals = new string[cfg.CatalogListView.Columns.Count];

            SetCatalogListView(rootEntries);
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

        private void SetCatalogListView(IEnumerable<RootEntry> rootEntries)
        {
            var count = 0;
            foreach (var rootEntry in rootEntries)
            {
                var itemColor = CreateRowValuesForRootEntry(_catalogVals, rootEntry, _listViewForeColor);
                _clientForm.AddCatalogListViewRow(_catalogVals, itemColor, rootEntry);
                ++count;
            }
            _clientForm.SetCatalogsLoadedStatus(count);
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
            _clientForm.SearchButtonEnable = false;
            var pattern = _clientForm.Pattern;
            var regexMode = _clientForm.RegexMode;
            if (regexMode)
            {
                var regexError = RegexHelper.GetRegexErrorMessage(pattern);
                if (!string.IsNullOrEmpty(regexError))
                {
                    MessageBox.Show(regexError);
                    return;
                }
            }

            _clientForm.AddSearchTextBoxAutoComplete(pattern);

            var resultEnum = Find.GetSearchHits(_rootEntries, pattern, regexMode, _clientForm.IncludePathInSearch);
            _searchResultList = resultEnum.ToList();
            _searchResultSortColumn = 0;
            _searchResultSortOrder = SortOrder.Ascending;
            _clientForm.SetSearchResultVirtualListSize(_searchResultList.Count);
            _clientForm.SearchButtonEnable = true;
        }

        public void SearchResultRetrieveVirtualItem()
        {
            var pairDirEntry = _searchResultList[_clientForm.SearchResultListViewItemIndex];
            var dirEntry = pairDirEntry.ChildDE;
            var itemColor = CreateRowValuesForDirEntry(_searchVals, dirEntry, _listViewForeColor);
            _searchVals[3] = pairDirEntry.ParentDE.FullPath;
            var lvi = _clientForm.BuildListViewItem(_searchVals, itemColor, pairDirEntry);
            _clientForm.SearchResultListViewItem = lvi;
        }

        public void DirectoryTreeViewAfterSelect()
        {
            var selectedNode = _clientForm.DirectoryTreeViewActiveAfterSelectNode;
            var commonEntry = (CommonEntry)selectedNode.Tag;
            var listSize = 0;
            _directoryList = null;
            _directoryListCommonEntry = null;
            if (commonEntry != null)
            {
                _directoryListCommonEntry = commonEntry;
                if (commonEntry.Children != null)
                {
                    _directoryList = commonEntry.Children.ToList(); // copy of list
                    _directoryList.Sort(DirectoryCompare);
                    listSize = commonEntry.Children.Count;
                }
                _clientForm.SetDirectoryPathTextbox = commonEntry.FullPath;
            }
            _clientForm.SetDirectoryVirtualListSize(listSize);
        }

        public void DirectoryRetrieveVirtualItem()
        {
            // VirtualItem wont be called if List size is zero so no check for null required.
            var dirEntry = _directoryList[_clientForm.DirectoryListViewItemIndex];
            var itemColor = CreateRowValuesForDirEntry(_directoryVals, dirEntry, _listViewForeColor);
            var lvi = _clientForm.BuildListViewItem(_directoryVals, itemColor, dirEntry);
            _clientForm.DirectoryListViewItem = lvi;
        }

        // before form closes capture any changed configuration.
        public void MyFormClosing()
        {
            _config.RecordConfig(_clientForm);
        }

        public void CatalogListViewItemActivate()
        {
            var newRoot = _clientForm.ActiveCatalogAfterSelectRootEntry;
            RootEntry currentRoot = null;
            if (_clientForm.DirectoryTreeViewNodes != null)
            {
                currentRoot = (RootEntry)_clientForm.DirectoryTreeViewNodes.Tag;
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
            _directorySortColumn = 0;
            _directorySortOrder = SortOrder.Ascending;
            return newRootNode;
        }

        public void DirectoryListViewItemActivate()
        {
            // Have activated on a entry in Directory List View.
            // If its a folder then change the tree view to select this folder.
            var dirEntry = _directoryList[_clientForm.ActiveDirectoryListViewIndexAfterActivate];
            if (dirEntry.IsDirectory)
            {
                SetDirectoryWithExpand(dirEntry);
            }
        }

        private void SetDirectoryWithExpand(DirEntry dirEntry)
        {
            var activatedDirEntryList = DirEntry.GetListFromRoot(dirEntry);

            var currentRootNode = _clientForm.DirectoryTreeViewNodes;
            RootEntry currentRoot = null;
            if (currentRootNode != null)
            {
                currentRoot = (RootEntry)currentRootNode.Tag;
            }
            TreeNode workingTreeNode = null;
            RootEntry newRoot = null;
            foreach (var entry in activatedDirEntryList)
            {
                if (newRoot == null)
                {
                    newRoot = (RootEntry)entry;
                    if (currentRoot != newRoot)
                    {
                        currentRootNode = SetNewDirectoryRoot(newRoot);
                        currentRoot = newRoot;
                    }
                    workingTreeNode = currentRootNode; // starting at rootnode.
                }
                else
                {
                    if (((DirEntry)entry).IsDirectory && workingTreeNode != null)
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
                _clientForm.DirectoryListViewDeselectItems();
                _clientForm.SelectDirectoryPane();
            }
        }

        public void SearchResultListViewItemActivate()
        {
            var pairDirEntry = _searchResultList[_clientForm.ActiveSearchResultIndexAfterActivate];
            var dirEntry = pairDirEntry.ChildDE;
            SetDirectoryWithExpand(dirEntry);

            if (!dirEntry.IsDirectory)
            {   // select the file in list view, do i have to scroll to it as well ???
                var index = _directoryList.IndexOf(dirEntry);
                _clientForm.SelectDirectoryListViewItem(index);
            }
            //MessageBox.Show(pairDirEntry.RootDE.Path + " ++ " + pairDirEntry.ParentDE.FullPath + " == " + pairDirEntry.ChildDE.Path);
        }

        public void DirectoryListViewItemSelectionChanged()
        {
            var indices = _clientForm.DirectorySelectedIndices;
            var indicesCount = _clientForm.DirectorySelectedIndicesCount;
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
            if (_searchResultList == null)
            {
                return;
            }
            var column = _clientForm.SearchResultListViewColumnIndex;
            if (_searchResultSortColumn == column)
            {
                _searchResultSortOrder = _searchResultSortOrder == SortOrder.Ascending 
                            ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _searchResultSortColumn = column;
                _searchResultSortOrder = SortOrder.Ascending;
            }
            _searchResultList.Sort(SearchResultCompare);
            _clientForm.ForceDrawSearchResultListView();
        }

        private int SearchResultCompare (PairDirEntry pde1, PairDirEntry pde2)
        {
            int compareResult;
            var de1 = pde1.ChildDE;
            var de2 = pde2.ChildDE;
            switch (_searchResultSortColumn)
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
                    throw new Exception(string.Format("Problem column {0} not handled for sort.", _searchResultSortColumn));
            }
            if (_searchResultSortOrder == SortOrder.Descending)
            {
                compareResult *= -1;
            }
            return compareResult;
        }

        public void DirectoryListViewColumnClick()
        {
            if (_directoryList == null
                || _directoryList.Count == 0)
            {
                return;
            }
            var column = _clientForm.DirectoryListViewColumnIndex;
            if (_directorySortColumn == column)
            {
                _directorySortOrder = _directorySortOrder == SortOrder.Ascending 
                            ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _directorySortColumn = column;
                _directorySortOrder = SortOrder.Ascending;
            }
            _directoryList.Sort(DirectoryCompare);
            _clientForm.ForceDrawDirectoryListView();
        }

        private int DirectoryCompare(DirEntry de1, DirEntry de2)
        {
            int compareResult;
            switch (_directorySortColumn)
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
                    throw new Exception(string.Format("Problem column {0} not handled for sort.", _directorySortColumn));
            }
            if (_directorySortOrder == SortOrder.Descending)
            {
                compareResult *= -1;
            }
            return compareResult;
        }

    }
}