using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string DummyNodeName = "_dummyNode";
        private readonly IDisplayTreeFromRootForm _clientForm;
        private readonly List<RootEntry> _rootEntries;
        private Config _config;

        public DisplayTreeFromRootPresenter(IDisplayTreeFromRootForm form, List<RootEntry> rootEntries, Config config) : base(form)
        {
            _clientForm = form;
            _rootEntries = rootEntries;
            _config = config;
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

        public void LoadData()  // OnLoadData handler
        {
            _clientForm.TreeViewNodes = BuildRootNode();
        }

        private TreeNode BuildRootNode()
        {
            var rootEntry = _rootEntries == null ? null : _rootEntries.ElementAtOrDefault(0);
            if (rootEntry == null)
            {
                return null;
            }

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

        public void BeforeExpandNode()  // OnBeforeExpandNode handler
        {
            CreateNodesPreExpand(_clientForm.ActiveBeforeExpandNode);
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

        public void AfterSelect()  // OnAfterSelect handler
        {
            var selectedNode = _clientForm.ActiveAfterSelectNode;
            SetDirectoryListView((CommonEntry) selectedNode.Tag);
        }

        private const string ModifiedFieldFormat = "{0:yyyy/MM/dd HH:mm:ss}";
        readonly string[] _directoryCols = { "Name", "Size", "Modified" };
        readonly string[] _directoryVals = new string[3]; // hack 
        private readonly Color _listViewForeColor = Color.Black;
        private readonly Color _listViewDirForeColor = Color.Blue;
        readonly string[] _searchCols = { "Name", "Size", "Modified", "Fullpath" };
        readonly string[] _searchVals = new string[4]; // hack 
        private List<PairDirEntry> _searchResults;

        private void SetDirectoryListView(CommonEntry selectedDirEntry)
        {
            foreach (var dirEntry in selectedDirEntry.Children)
            {
                var itemColor = PopulateRowValues(_directoryVals, dirEntry, _listViewForeColor);
                _clientForm.AddDirectoryListViewRow(_directoryVals, itemColor, dirEntry);
            }
        }

        private Color PopulateRowValues(string[] vals, DirEntry dirEntry, Color itemColor)
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
            _clientForm.SetSearchVirtualList(_searchResults);
        }

        public void SearchResultRetrieveVirtualItem()
        {
            var pairDirEntry = _searchResults[_clientForm.SearchResultListViewItemIndex];
            var dirEntry = pairDirEntry.ChildDE;
            var itemColor = PopulateRowValues(_searchVals, dirEntry, _listViewForeColor);
            _searchVals[3] = pairDirEntry.ParentDE.FullPath;
            var lvi = _clientForm.BuildListViewItem(_searchVals, itemColor, pairDirEntry);
            _clientForm.SearchResultListViewItem = lvi;
        }

        // before form closes, with reason why if i bother to capture it.
        public void MyFormClosing()
        {
            _config.CaptureConfig(_clientForm);
        }
    }
}