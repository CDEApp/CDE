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

        /// <summary>
        /// Load RootEntry and first level directories into TreeView.
        /// </summary>
        public void LoadData()  // OnLoadData handler
        {
            _clientDisplayTreeFromRootFormForm.TreeViewNodes = BuildTopLevelTreeNodes();
        }

        private TreeNode BuildTopLevelTreeNodes()
        {
            var rootTreeNode = new TreeNode(_rootEntry.RootPath)
                                   {ImageIndex = 0, Tag = _rootEntry};
            if (_rootEntry.Children != null)
            {
                foreach (var dirEntry in _rootEntry.Children)
                {
                    if (dirEntry.IsDirectory)
                    {
                        var entry = rootTreeNode.Nodes.Add(dirEntry.Name);
                        //entry.ImageIndex = 0;
                        entry.Tag = dirEntry;

                        AddChildrenToTreeNode(entry, dirEntry);
                    }
                }
            }
            return rootTreeNode;
        }

        private static void AddChildrenToTreeNode(TreeNode treeNode, CommonEntry dirEntry)
        {
            foreach (var subDirEntry in dirEntry.Children)
            {
                if (subDirEntry.IsDirectory)
                {
                    var subEntry = treeNode.Nodes.Add(subDirEntry.Name);
                    //subEntry.ImageIndex = 0;
                    subEntry.Tag = subDirEntry;
                }
            }
        }

        public void BeforeExpandNode()  // OnBeforeExpandNode handler
        {
            var expandNode = _clientDisplayTreeFromRootFormForm.ActiveBeforeExpandNode;
            foreach (TreeNode treeNode in expandNode.Nodes)
            {
                foreach (TreeNode subTreeNode in treeNode.Nodes)
                {
                    var subDirEntry = (DirEntry)subTreeNode.Tag;
                    if (subDirEntry.IsDirectory)
                    {
                        // if subTreeNode.Nodes allready has members its had children done.
                        if (subTreeNode.Nodes.Count == 0 && subDirEntry.Children.Count > 0)
                        {
                            AddChildrenToTreeNode(subTreeNode, subDirEntry);
                        }
                    }
                }
            }

            // conversely - when collapse remove nodes no longer required at more than 1 level away ?
            // No for collapse just leave nodes that way expand puts back tree in previous expanded state.
        }


    }

}