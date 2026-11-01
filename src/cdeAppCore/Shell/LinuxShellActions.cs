using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// Linux <see cref="IShellActions"/>. Open uses <c>xdg-open</c>. Explore and Properties call the
/// <c>org.freedesktop.FileManager1</c> D-Bus service through <c>dbus-send</c> (no D-Bus library, which keeps the
/// single-file sidecar small); Explore falls back to opening the parent folder, Properties has no fallback.
/// </summary>
public sealed class LinuxShellActions : UnixShellActions
{
    public LinuxShellActions(
        IReadOnlyList<CustomCommandOptions> customCommands,
        ICommandRunner runner = null,
        ShellCommandOverrides overrides = null,
        ILogger logger = null)
        : base(customCommands, runner ?? new ProcessCommandRunner(), overrides, logger)
    {
    }

    protected override ShellResult DefaultOpen(string fullPath) => Run("xdg-open", [fullPath], wait: false);

    protected override ShellResult DefaultExplore(string fullPath)
    {
        var shown = Run("dbus-send", FileManagerCall("ShowItems", fullPath), wait: true);
        if (shown.IsOk)
        {
            return shown;
        }

        var slash = fullPath.LastIndexOf('/');
        if (slash < 0)
        {
            return shown;
        }

        var parent = slash == 0 ? "/" : fullPath[..slash];
        return Run("xdg-open", [parent], wait: false);
    }

    protected override ShellResult DefaultProperties(string fullPath)
    {
        var shown = Run("dbus-send", FileManagerCall("ShowItemProperties", fullPath), wait: true);
        return shown.IsOk
            ? shown
            : ShellResult.Failed("Properties are unavailable: no file manager answered on the session bus");
    }

    private static string[] FileManagerCall(string method, string fullPath) =>
    [
        "--session",
        "--print-reply",
        "--reply-timeout=2000",
        "--dest=org.freedesktop.FileManager1",
        "--type=method_call",
        "/org/freedesktop/FileManager1",
        "org.freedesktop.FileManager1." + method,
        "array:string:" + FileUri(fullPath),
        "string:",
    ];

    /// <summary>
    /// <c>file://</c> URI with every path segment percent-encoded. dbus-send splits array elements on commas,
    /// and <see cref="Uri.EscapeDataString"/> encodes those too.
    /// </summary>
    public static string FileUri(string fullPath) =>
        "file://" + string.Join('/', fullPath.Split('/').Select(Uri.EscapeDataString));
}
