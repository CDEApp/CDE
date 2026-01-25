using System.Collections.Generic;
using ProtoBuf;

namespace cdeWin.Cfg;

[ProtoContract]
public class ListViewConfig
{
    [ProtoMember(1)]
    public List<ColumnConfig> Columns;

    /// <summary>
    /// Only capture column width changes, nothing else at the moment.
    /// </summary>
    public void RecordColumnWidths(IEnumerable<ColumnConfig> liveColumns)
    {
        foreach (var liveColumn in liveColumns)
        {
            var saveCol = Columns.Find(x => x.Name == liveColumn.Name);
            saveCol?.Width = liveColumn.Width;
        }
    }
}