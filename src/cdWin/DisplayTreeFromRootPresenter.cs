using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using cdeLib;
using cdeLib.Infrastructure;

namespace cdeWin
{
    public interface IDisplayTreeFromRootPresenter : IPresenter
    {
    }

    public class DisplayTreeFromRootPresenter : Presenter<IDisplayTreeFromRootForm>, IDisplayTreeFromRootPresenter
    {
        private const string ModifiedFieldFormat = "{0:yyyy/MM/dd HH:mm:ss}";
        private const string DummyNodeName = "_dummyNode";
        private readonly Color _listViewForeColor = Color.Black;
        private readonly Color _listViewDirForeColor = Color.Blue;

        private readonly IDisplayTreeFromRootForm _clientForm;
        private readonly List<RootEntry> _rootEntries;
        private readonly Config _config;

        private readonly string[] _directoryVals;
        private readonly string[] _searchVals;
        private readonly string[] _catalogVals;

        private List<PairDirEntry> _searchResults;
        private CommonEntry _directoryListViewCommonEntry;

        public DisplayTreeFromRootPresenter(IDisplayTreeFromRootForm form, List<RootEntry> rootEntries, Config config) : base(form)
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
            var rootTreeNode = NewTreeNode(rootEntry, rootEntry.RootPath);
            SetDummyChildNode(rootTreeNode, rootEntry);
            return rootTreeNode;
        }

        /// <summary>
        /// A node with children gets a dummy child node to display as an expandable node.
        /// </summary>
        private static void SetDummyChildNode(TreeNode treeNode, CommonEntry commonEntry)
        {
            if (commonEntry.Children.Any(entry => entry.IsDirectory))
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
                var newTreeNode = NewTreeNode(dirEntry, dirEntry.Name);
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
            vals[0] = dirEntry.Name;
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
            foreach (var rootEntry in rootEntries)
            {
                var itemColor = CreateRowValuesForRootEntry(_catalogVals, rootEntry, _listViewForeColor);
                _clientForm.AddCatalogListViewRow(_catalogVals, itemColor, rootEntry);
            }
        }

        private Color CreateRowValuesForRootEntry(string[] vals, RootEntry rootEntry, Color listViewForeColor)
        {
            vals[0] = rootEntry.RootPath;
            vals[1] = rootEntry.VolumeName;
            vals[2] = rootEntry.DirCount.ToString();
            vals[3] = rootEntry.FileCount.ToString();
            vals[4] = (rootEntry.DirCount + rootEntry.FileCount).ToString();
            vals[5] = rootEntry.DriveLetterHint;
            vals[6] = rootEntry.AvailSpace.ToHRString();
            vals[7] = rootEntry.UsedSpace.ToHRString();
            vals[8] = string.Format(ModifiedFieldFormat, rootEntry.ScanStartUTC);
            vals[9] = rootEntry.Description;

            return listViewForeColor;
        }

        public void SearchRoots()
        {
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
            _searchResults = resultEnum.ToList();
            _clientForm.SetSearchResultVirtualList(_searchResults);
        }

        public void SearchResultRetrieveVirtualItem()
        {
            var pairDirEntry = _searchResults[_clientForm.SearchResultListViewItemIndex];
            var dirEntry = pairDirEntry.ChildDE;
            var itemColor = CreateRowValuesForDirEntry(_searchVals, dirEntry, _listViewForeColor);
            _searchVals[3] = pairDirEntry.ParentDE.FullPath;
            var lvi = _clientForm.BuildListViewItem(_searchVals, itemColor, pairDirEntry);
            _clientForm.SearchResultListViewItem = lvi;
        }

        public void DirectoryTreeViewAfterSelect()
        {
            var selectedNode = _clientForm.DirectoryTreeViewActiveAfterSelectNode;
            _directoryListViewCommonEntry = (CommonEntry)selectedNode.Tag;
            _clientForm.SetDirectoryVirtualList(_directoryListViewCommonEntry);
        }

        public void DirectoryRetrieveVirtualItem()
        {
            var dirEntry = _directoryListViewCommonEntry.Children[_clientForm.DirectoryListViewItemIndex];
            var itemColor = CreateRowValuesForDirEntry(_directoryVals, dirEntry, _listViewForeColor);
            var lvi = _clientForm.BuildListViewItem(_directoryVals, itemColor, dirEntry);
            _clientForm.DirectoryListViewItem = lvi;
        }

        // before form closes capture any changed configuration.
        public void MyFormClosing()
        {
            _config.CaptureConfig(_clientForm);
        }

        public void CatalogListViewItemActivate()
        {
            _clientForm.DirectoryTreeViewNodes = BuildRootNode(_clientForm.ActiveCatalogAfterSelectRootEntry);
            // TODO change the Active Tab in interface I Think go to directory tree to see it.
        }
    }
}