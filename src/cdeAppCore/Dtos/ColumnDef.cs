namespace cdeAppCore.Dtos;

/// <summary>
/// A serializable column definition for a frontend list view: a stable <see cref="Key"/>, the
/// display <see cref="Header"/>, and a default <see cref="Width"/>. Lets the core supply default
/// column sets that a frontend's UI-state can override (widths, ordering).
/// </summary>
public sealed class ColumnDef
{
    public string Key { get; set; }
    public string Header { get; set; }
    public int Width { get; set; }
}
