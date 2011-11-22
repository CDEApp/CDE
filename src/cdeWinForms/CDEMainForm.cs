using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeLib;
using cdeWinFormsPresenter;

namespace cdeWinForms
{

    public partial class CDEMainForm : Form, ICDEMainView
    {
        public CDEMainForm()
        {
            InitializeComponent();
        }

        private ICDEMainPresenterCallBacks _presenter;
        public void Attach(ICDEMainPresenterCallBacks presenter)
        {
            _presenter = presenter;
            //button1.Click += delegate(object sender, EventArgs e) { _presenter.OnButtonClick(); }; // not using lambda
            button1.Click += (sender, e) => _presenter.OnButtonClick();
        }

        public bool ButtonEnabled
        {
            get { return button1.Enabled; }
            set { button1.Enabled = value; }
        }

        public string ButtonText
        {
            get { return button1.Text; }
            set { button1.Text = value; }
        }

        public IList<RootEntry> GetCatalogs()
        {
            throw new NotImplementedException();
        }
    }
}
