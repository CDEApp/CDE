using System.Collections.Generic;
using System.Windows.Forms;

namespace cdeWin
{
    public static class ListViewExtensions
    {
        public static void SetColumnHeaders(this ListView lv, IEnumerable<ColumnConfig> columns)
        {
            lv.Clear();
            if (columns != null)
            {
                foreach (var col in columns)
                {
                    lv.Columns.Add(col.Name, col.Width, col.Alignment);
                }
            }
        }
    }
}