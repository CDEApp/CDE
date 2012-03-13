using System;
using System.Windows.Forms;

namespace cdeWin
{
    public partial class MyAboutBox : Form
    {
        public MyAboutBox()
        {
            InitializeComponent();
        }

        public static void MyShow(Form parentForm)
        {
            var m = new MyAboutBox();
            m.ShowDialog(parentForm);
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
