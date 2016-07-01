using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    public static class ColumnHeaderCollectionExtension
    {
        public static IEnumerable<ColumnConfig> ColumnConfigs(this ListView.ColumnHeaderCollection chc)
        {
            var columns = chc.OfType<ColumnHeader>();
            return columns.Select(columnHeader =>
                new ColumnConfig
                {
                    Name = columnHeader.Text,
                    Width = columnHeader.Width
                });
        }
    }
}