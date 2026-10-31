using System;
using System.Drawing;
using System.Windows.Forms;

namespace cdeWin;

/// <summary>Help &gt; Search Syntax: a read-only dialog with the Everything-style operators, modifiers and filters.</summary>
public sealed class SearchSyntaxForm : Form
{
    public SearchSyntaxForm()
    {
        Text = "Search Syntax (Everything mode)";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(880, 660);

        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 10f),
            Text = SearchSyntaxHelp.ToText(),
            BackColor = SystemColors.Window,
        };
        Controls.Add(box);
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        Shown += (_, _) => box.Select(0, 0); // no pre-selected text
    }

    public static void MyShow(Form parentForm)
    {
        using var form = new SearchSyntaxForm();
        form.ShowDialog(parentForm);
    }
}
