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
            var n = parentNode.Nodes[0];
            var b = n.Nodes;
            if (parentNode.Nodes.Count == 1 && parentNode.Nodes[0].Text == DummyNodeName)
            {
                // Replace Dummy with real nodes now that will be visible.
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
    }

}