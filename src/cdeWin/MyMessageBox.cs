using System;
using System.Windows.Forms;

namespace cdeWin;

public partial class MyMessageBox : Form
{
    public MyMessageBox()
    {
        InitializeComponent();
    }

    // ReSharper disable UseObjectOrCollectionInitializer
    public static void MyShow(Form parentForm, string message)
    {
        var m = new MyMessageBox();
        m.messageTextBox.Text = message;
        m.messageTextBox.Select(0, 0);  // do not show text selected.
        m.ShowDialog(parentForm);
    }
    // ReSharper restore UseObjectOrCollectionInitializer

    private void OkButtonClick(object sender, EventArgs e)
    {
        Close();
    }
}