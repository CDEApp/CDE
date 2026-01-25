using System.Drawing;
using System.Windows.Forms;
using ProtoBuf;

namespace cdeWin.Cfg;

[ProtoContract]
public class WindowConfig
{
    /// <summary>
    /// -1 means don't set this value.
    /// </summary>
    [ProtoMember(1)]
    public int Left;

    /// <summary>
    /// -1 means don't set this value.
    /// </summary>
    [ProtoMember(2)]
    public int Top;

    [ProtoMember(3)]
    public int Width;

    [ProtoMember(4)]
    public int Height;

    [ProtoMember(5)]
    public FormWindowState WindowState;

    public void RestoreFormTopLeft(Form form)
    {
        if (Left != -1 && Top != -1)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(Left, Top);
        }
    }

    public void RestoreForm(Form form)
    {
        var restoreRect = new Rectangle(Left, Top, Width, Height);

        if (Left != -1 && Top != -1 && restoreRect.IsVisibleOnAnyScreen())
        {   // Position sanity check before we allow manual restore.
            form.StartPosition = FormStartPosition.Manual;
        }

        form.WindowState = FormWindowState.Normal;
        form.DesktopBounds = restoreRect;
        form.WindowState = WindowState;
    }

    public void RecordForm(Form form)
    {
        WindowState = form.WindowState;

        var effectiveBounds = form.DesktopBounds;
        if (WindowState != FormWindowState.Normal)
        {
            effectiveBounds = form.RestoreBounds;
        }

        Left = effectiveBounds.X;
        Top = effectiveBounds.Y;
        Width = effectiveBounds.Width;
        Height = effectiveBounds.Height;
    }
}