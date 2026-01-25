using System.ComponentModel;
using System.Windows.Forms;
using ProtoBuf;

namespace cdeWin.Cfg;

[ProtoContract]
public class ColumnConfig
{
    [ProtoMember(1)]
    public int Width;
    [ProtoMember(2)]
    public string Name;
    [ProtoMember(3)]
    [DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment Alignment = HorizontalAlignment.Left;

    // columns cannot currently be hidden.
    // columns cannot currently be reordered.
}