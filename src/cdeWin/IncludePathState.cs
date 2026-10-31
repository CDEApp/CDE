namespace cdeWin;

/// <summary>
/// What the Include Path combo shows. Everything mode reaches the path with <c>path:</c> in the query, so the combo
/// shows "Exclude Path from Search" greyed out. The user's choice is kept in <see cref="Saved"/>, which is what config
/// records, and is shown again when Everything is switched off.
/// </summary>
public sealed class IncludePathState
{
    private bool _everything;

    /// <summary>The user's choice. The default is to include the path.</summary>
    public bool Saved { get; private set; } = true;

    /// <summary>Whether the combo shows "Include Path in Search".</summary>
    public bool Shown => !_everything && Saved;

    public bool ComboEnabled => !_everything;

    public bool EverythingMode => _everything;

    public void SetEverything(bool on) => _everything = on;

    /// <summary>Record a choice (the user's pick, or a restored config value).</summary>
    public void Choose(bool includePath) => Saved = includePath;
}
