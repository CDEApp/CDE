using System.Collections.Generic;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// macOS <see cref="IShellActions"/>. Open is <c>open</c>, Explore is <c>open -R</c> (reveals and selects in Finder).
/// Finder has no command-line "get info", so Properties asks Finder through <c>osascript</c>.
/// </summary>
public sealed class MacShellActions : UnixShellActions
{
    // The path arrives as an argv item, never spliced into the script text.
    private const string InformationScript =
        "on run argv\n" +
        "tell application \"Finder\"\n" +
        "open information window of (POSIX file (item 1 of argv) as alias)\n" +
        "activate\n" +
        "end tell\n" +
        "end run";

    public MacShellActions(
        IReadOnlyList<CustomCommandOptions> customCommands,
        ICommandRunner runner = null,
        ShellCommandOverrides overrides = null,
        ILogger logger = null)
        : base(customCommands, runner ?? new ProcessCommandRunner(), overrides, logger)
    {
    }

    protected override ShellResult DefaultOpen(string fullPath) => Run("open", [fullPath], wait: true);

    protected override ShellResult DefaultExplore(string fullPath) => Run("open", ["-R", fullPath], wait: true);

    protected override ShellResult DefaultProperties(string fullPath) =>
        Run("osascript", ["-e", InformationScript, fullPath], wait: true);
}
