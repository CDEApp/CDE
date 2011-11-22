using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    /* Passive view hackery http://cre8ivethought.com/blog/2009/12/19/using-conventions-with-passive-view */

    public interface IDisplayTreeFromRootForm : IView
    {
        // this doesnt address incremental update,
        // but that doesnt matter for cde...

        //IEnumerable<ClientReport> Clients { get; set; }
        //ClientReport GetSelectedClient();

        event EventAction OnLoadData;
        event EventAction OnBeforeExpandNode;

        TreeNode TreeViewNodes { set; }
        TreeNode ListViewStuff { set; }

        TreeNode ActiveBeforeExpandNode { get; }
    }

    public partial class DisplayTreeFromRootFormForm : Form, IDisplayTreeFromRootForm
    {
        public DisplayTreeFromRootFormForm()
        {
            InitializeComponent();
            RegisterCLientEvents();
        }

        private void RegisterCLientEvents()
        {
            btnLoadData.Click += (s, e) => OnLoadData();
            tvMain.BeforeExpand += (s, e) => {
                ActiveBeforeExpandNode = e.Node;
                OnBeforeExpandNode();
            };

        }

        public event EventAction OnLoadData;
        public event EventAction OnBeforeExpandNode;
        public TreeNode ActiveBeforeExpandNode { get; private set; }

        /// <summary>
        /// Adds nodes to tree wrapped by BeginUpdate EndUpdate.
        /// </summary>
        public TreeNode TreeViewNodes
        {
            set
            {
                tvMain.BeginUpdate();
                tvMain.Nodes.Add(value);
                tvMain.SelectedNode = tvMain.Nodes[0];
                ////tvMain.Nodes[0].Expand();
                //tvMain.Select();
                tvMain.EndUpdate();
            }
        }

        public TreeNode ListViewStuff
        {
            set
            {
                var a = lvEntries.DataBindings;
                var b = lvEntries.Items;
                var c = value;
                // to use databindings i need to build a barfy dataset with icky internal referency crap pass.
            }
        }

    }
}
