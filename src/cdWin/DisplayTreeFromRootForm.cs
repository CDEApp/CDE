using System.Drawing;
using System.Windows.Forms;

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
        event EventAction OnAfterSelect;

        TreeNode TreeViewNodes { get;  set; }
        //TreeNode ListViewStuff { set; }

        TreeNode ActiveBeforeExpandNode { get; set; }
        TreeNode ActiveAfterSelectNode { get; set; }
        bool CancelExpandEvent { get; set; }

        void SetColumnHeaders(string[] cols);
        void AddListViewRow(string[] vals, Color firstColumnForeColor, object tag);

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
            lvEntries.View = View.Details; // detail 

            btnLoadData.Click += (s, e) => OnLoadData();

            CancelExpandEvent = false;
            tvMain.BeforeExpand += (s, e) => 
            {
                ActiveBeforeExpandNode = e.Node;
                OnBeforeExpandNode();
                e.Cancel = CancelExpandEvent;
                CancelExpandEvent = false;
            };

            tvMain.AfterSelect += (s, e) => 
            {
                ActiveAfterSelectNode = e.Node;
                OnAfterSelect();
            };
        }

        public event EventAction OnLoadData;
        public event EventAction OnBeforeExpandNode;
        public event EventAction OnAfterSelect;
        public TreeNode ActiveBeforeExpandNode { get; set; }
        public TreeNode ActiveAfterSelectNode { get; set; }

        /// <summary>
        /// One shot cancel next expand in BeforExpand, then sets back to false.
        /// </summary>
        public bool CancelExpandEvent { get; set; }

        /// <summary>
        /// Adds nodes to tree wrapped by BeginUpdate EndUpdate.
        /// </summary>
        public TreeNode TreeViewNodes
        {
            get { return tvMain.Nodes[0]; } // Assumption just one root node.

            set
            {
                //AddColumnHeaders();
                tvMain.BeginUpdate();
                tvMain.Nodes.Add(value);
                tvMain.SelectedNode = tvMain.Nodes[0];
                tvMain.Nodes[0].Expand();
                tvMain.Select();
                tvMain.EndUpdate();

                // TODO set the value of the detail pane. lvEntries
            }
        }

        //public TreeNode ListViewStuff
        //{
        //    set
        //    {
        //        var a = lvEntries.DataBindings;
        //        var b = lvEntries.Items;
        //        var c = value;
        //    }
        //}

        /// <summary>
        /// Clear list view and set column headers.
        /// </summary>
        /// <param name="cols"></param>
        public void SetColumnHeaders(string[] cols)
        {
            lvEntries.Clear();
            //lvEntries.Items.Clear();
            foreach (var col in cols)
            {
                lvEntries.Columns.Add(col, 100, HorizontalAlignment.Left);
            }
        }

        public void AddListViewRow(string[] vals, Color firstColumnForeColor, object tag)
        {
            var lvItem = new ListViewItem(vals[0]);     // first val
            lvItem.UseItemStyleForSubItems = false;     // make styling apply.
            lvItem.SubItems[0].ForeColor = firstColumnForeColor;   // style our name entries
            lvItem.Tag = tag;
            for (var i = 1; i < vals.Length; ++i)       // the rest of vals
            {
                var s = lvItem.SubItems.Add(vals[i]);
            }
            lvEntries.Items.Add(lvItem);
        }
    }
}
