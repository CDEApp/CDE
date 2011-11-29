using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    public interface IDisplayTreeFromRootPresenter : IPresenter
    {
    }

    public class DisplayTreeFromRootPresenter : Presenter<IDisplayTreeFromRootForm>, IDisplayTreeFromRootPresenter
    {
        private const string DummyNodeName = "_dummyNode";
        private readonly IDisplayTreeFromRootForm _clientDisplayTreeFromRootFormForm;
        private readonly RootEntry _rootEntry;

        public DisplayTreeFromRootPresenter(IDisplayTreeFromRootForm displayTreeFromRootFormForm, RootEntry rootEntry) : base(displayTreeFromRootFormForm)
        {
            _clientDisplayTreeFromRootFormForm = displayTreeFromRootFormForm;
            _rootEntry = rootEntry;
        }

        public void Display()
        {
            try
            {
                _clientDisplayTreeFromRootFormForm.ShowDialog();
            }
            finally
            {
                _clientDisplayTreeFromRootFormForm.Dispose();
            }
        }

        public void LoadData()  // OnLoadData handler
        {
            _clientDisplayTreeFromRootFormForm.TreeViewNodes = BuildRootNode();
        }

        private TreeNode BuildRootNode()
        {
            var rootTreeNode = NewTreeNode(_rootEntry, _rootEntry.RootPath);
            SetDummyChildNode(rootTreeNode, _rootEntry);
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
            CreateNodesPreExpand(_clientDisplayTreeFromRootFormForm.ActiveBeforeExpandNode);
        }

        private static void CreateNodesPreExpand(TreeNode parentNode)
        {
            if (parentNode.Nodes.Count == 1 && parentNode.Nodes[0].Text == DummyNodeName)
            {
                // Replace Dummy with real nodes now visible.
                parentNode.Nodes.Clear();
                AddAllDirectoriesChildren(parentNode, (CommonEntry) parentNode.Tag);
            }
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
            var selectedNode = _clientDisplayTreeFromRootFormForm.ActiveAfterSelectNode;
            SetListView((CommonEntry) selectedNode.Tag);
        }

        private const string ModifiedFieldFormat = "{0:yyyy/MM/dd HH:mm:ss}";
        readonly string[] _cols = { "Name", "Size", "Modified" };
        readonly string[] _vals = new string[3]; // hack 
        private readonly Color _listViewForeColor = Color.Black;
        private readonly Color _listViewDirForeColor = Color.Blue;

        private void SetListView(CommonEntry selectedDirEntry)
        {
            _clientDisplayTreeFromRootFormForm.SetColumnHeaders(_cols);
            foreach (var dirEntry in selectedDirEntry.Children)
            {
                Color itemColor = _listViewForeColor;

                _vals[0] = dirEntry.Name;
                _vals[1] = dirEntry.Size.ToString(); 
                if (dirEntry.IsDirectory)
                {
                    itemColor = _listViewDirForeColor;
                    _vals[1] = "<Dir>";
                }
                //_vals[1] = dirEntry.IsDirectory ? "<Dir>" : dirEntry.Size.ToString();
                _vals[2] = dirEntry.IsModifiedBad ? "<Bad Date>" : string.Format(ModifiedFieldFormat, dirEntry.Modified);
                _clientDisplayTreeFromRootFormForm.AddListViewRow(_vals, itemColor, dirEntry);
            }
        }
    }

}