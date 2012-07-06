using System;
using System.Windows.Forms;

namespace cdeWin
{
    public partial class MyAboutBox : Form
    {
		private IConfig _config;

        public MyAboutBox(IConfig config)
        {
            InitializeComponent();
        	_config = config;
			linkEmail.Text = _config.ContactEmail;
			tbVersion.Text = String.Format("{0} v{1}", _config.ProductName, _config.Version);
        }

        public static void MyShow(Form parentForm, IConfig config)
        {
			var m = new MyAboutBox(config);
            m.ShowDialog(parentForm);
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            Close();
        }

		private void linkEmail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = string.Format("mailto:{0}?subject=About {1} v{2}",
					_config.ContactEmail, _config.ProductName, _config.Version);
			proc.Start();

		}

		private void MyAboutBox_Load(object sender, EventArgs e)
		{
			Text = String.Format("About {0} v{1}", _config.ProductName, _config.Version);
		}

    }
}
