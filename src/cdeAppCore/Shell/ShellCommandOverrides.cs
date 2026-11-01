namespace cdeAppCore.Shell;

/// <summary>
/// User replacements for the built-in Open / Explore / Properties verbs (<c>AppConfig:Shell</c>). A verb whose
/// <see cref="CustomCommandOptions.Command"/> is empty keeps the auto-detected behaviour. Linux and macOS only;
/// Windows keeps its native shell calls.
/// </summary>
public class ShellCommandOverrides
{
    public CustomCommandOptions OpenCommand { get; set; } = new() { Arguments = "{path}" };
    public CustomCommandOptions ExploreCommand { get; set; } = new() { Arguments = "{path}" };
    public CustomCommandOptions PropertiesCommand { get; set; } = new() { Arguments = "{path}" };
}
