namespace cdeWin;

/// <summary>
/// What the Files/Folders combo shows. Everything mode searches both kinds and takes <c>file:</c> and
/// <c>folder:</c> from the query, so the combo shows "Files and Folders" (index 0) greyed out. The user's choice is
/// kept in <see cref="Saved"/>, which is what config records, and is shown again when Everything is switched off.
/// </summary>
public sealed class FindEntryFilterState
{
    private bool _everything;

    /// <summary>The user's choice: 0 Files and Folders, 1 Files Only, 2 Folders Only.</summary>
    public int Saved { get; private set; }

    /// <summary>The combo index to display.</summary>
    public int Shown => _everything ? 0 : Saved;

    public bool ComboEnabled => !_everything;

    public bool EverythingMode => _everything;

    public void SetEverything(bool on) => _everything = on;

    /// <summary>Record a choice (the user's pick, or a restored config value).</summary>
    public void Choose(int index) => Saved = index;
}
