using System.Collections.Generic;

namespace cdeAppCore.Shell;

/// <summary>
/// The narrow set of OS shell operations a frontend can ask the host to perform against a
/// <em>resolved local filesystem path</em>: the fixed verbs <see cref="Open"/>, <see cref="Explore"/>
/// and <see cref="ShowProperties"/>, plus a custom-command facility (the configured
/// <see cref="CustomCommandOptions"/> list and <see cref="RunCustomCommand"/>, which performs
/// <c>{path}</c>/<c>{dir}</c>/<c>{filename}</c> substitution before launching).
/// </summary>
/// <remarks>
/// Clipboard copy, select-all, view-in-tree and go-to-parent are deliberately NOT here — they are
/// frontend-native concerns; core only supplies the path strings they need. Path resolution and the
/// <c>ExistsOnFileSystem</c> capability guard live in the session/host, not in this interface.
/// </remarks>
public interface IShellActions
{
    /// <summary>The configured custom commands, in order, for a frontend to render as menu items.</summary>
    IReadOnlyList<CustomCommandOptions> CustomCommands { get; }

    /// <summary>Open a file/folder with its default shell association (<c>UseShellExecute</c>).</summary>
    void Open(string fullPath);

    /// <summary>Reveal an entry in the OS file explorer (selecting it).</summary>
    void Explore(string fullPath);

    /// <summary>Show the OS "properties" dialog for an entry.</summary>
    void ShowProperties(string fullPath);

    /// <summary>
    /// Run a configured custom command (addressed by its index in <see cref="CustomCommands"/>)
    /// against a resolved local path, substituting the path tokens in its arguments template.
    /// </summary>
    void RunCustomCommand(int commandIndex, string fullPath);

    /// <summary>Run an explicit custom command against a resolved local path (in-process callers).</summary>
    void RunCustomCommand(CustomCommandOptions command, string fullPath);
}
