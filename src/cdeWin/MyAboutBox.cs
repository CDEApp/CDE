using System;
using System.Windows.Forms;

namespace cdeWin
{
    public partial class MyAboutBox : Form
    {
		private readonly IConfig _config;

        public MyAboutBox(IConfig config)
        {
            InitializeComponent();
        	_config = config;
			linkEmail.Text = _config.ContactEmail;
			tbVersion.Text = $"{_config.ProductName} v{_config.Version}";
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
		    try
		    {
                var proc = new System.Diagnostics.Process
                {
                    StartInfo =
                    {
                        FileName =
                            $"mailto:{_config.ContactEmail}?subject=About {_config.ProductName} v{_config.Version}"
                    }
                };
                proc.Start();
		    }
		    catch
            {
            }
		}

		private void MyAboutBox_Load(object sender, EventArgs e)
		{
			Text = $"About {_config.ProductName} v{_config.Version}";
		}
    }
}
