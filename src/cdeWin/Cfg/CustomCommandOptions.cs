namespace cdeWin.Cfg;

/// <summary>
/// A single user-configured external command shown in the file/folder context menus.
/// <see cref="Arguments"/> is a template; the <c>{path}</c>, <c>{dir}</c> and <c>{filename}</c>
/// tokens are substituted from the selected entry before the command is launched.
/// </summary>
public class CustomCommandOptions
{
    public string Label { get; set; } = "";
    public string Command { get; set; } = "";
    public string Arguments { get; set; } = "{path}";
}
