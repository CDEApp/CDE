using System.Collections.Generic;
using System.Windows.Forms;
using cdeWin.Cfg;

namespace cdeWin;

/// <summary>
/// Double Buffered List View - removes scroll flicker.
/// Added SetColumnHeaders() convenience.
/// Source http://stackoverflow.com/a/3886695
/// </summary>
public class DoubleBufferListView : ListView
{
    public DoubleBufferListView()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    public void SetColumnHeaders(IEnumerable<ColumnConfig> columns)
    {
        Clear();
        if (columns == null) return;
        foreach (var col in columns)
        {
            Columns.Add(col.Name, col.Width, col.Alignment);
        }
    }
}