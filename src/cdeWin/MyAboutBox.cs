using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace cdeWin;

public partial class MyAboutBox : Form
{
    private readonly IConfig _config;

    public MyAboutBox(IConfig config)
    {
        InitializeComponent();
        _config = config;
        linkRepository.Text = _config.LinkRepository;
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

    private void linkRepository_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_config.LinkRepository) { UseShellExecute = true });
        }
        catch
        {
            // ignored
        }
    }

    private void MyAboutBox_Load(object sender, EventArgs e)
    {
        Text = $"About {_config.ProductName} v{_config.Version}";
    }
}