using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// Windows implementation of <see cref="IShellActions"/>, lifted from the WinForms
/// <c>WindowsExplorerUtilities</c>/<c>CommandTokens</c> and de-static-ified: the custom-command list
/// and the logger are injected (no <c>Program.Configuration</c> static, no WinForms <c>MessageBox</c>
/// — launch failures are logged). <see cref="ShowProperties"/> passes <c>hwnd = IntPtr.Zero</c>, so it
/// runs identically whether hosted in the WinForms app or headless in the API sidecar.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsShellActions : IShellActions
{
    private readonly ILogger _logger;

    public WindowsShellActions(IReadOnlyList<CustomCommandOptions> customCommands, ILogger logger)
    {
        CustomCommands = customCommands ?? [];
        _logger = logger;
    }

    public IReadOnlyList<CustomCommandOptions> CustomCommands { get; }

    public ShellResult Open(string fullPath) => Launch("Open", () =>
    {
        using var p = new Process { StartInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true } };
        p.Start();
        return true;
    });

    public ShellResult Explore(string fullPath) => Launch("Explore", () =>
    {
        Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "/select,\"" + fullPath + "\"");
        return true;
    });

    public ShellResult ShowProperties(string fullPath) => Launch("Properties", () =>
    {
        var info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = fullPath;
        info.nShow = SW_SHOW;
        info.fMask = SEE_MASK_INVOKEIDLIST;
        return ShellExecuteEx(ref info);
    });

    public ShellResult RunCustomCommand(int commandIndex, string fullPath)
    {
        if (commandIndex < 0 || commandIndex >= CustomCommands.Count)
        {
            return ShellResult.Failed($"no custom command {commandIndex}");
        }

        return RunCustomCommand(CustomCommands[commandIndex], fullPath);
    }

    public ShellResult RunCustomCommand(CustomCommandOptions command, string fullPath)
    {
        if (command == null || string.IsNullOrEmpty(command.Command))
        {
            return ShellResult.Failed("the custom command has no program set");
        }

        var args = CommandTokens.Substitute(command.Arguments ?? "", fullPath);
        return Launch(command.Command, () =>
        {
            Process.Start(command.Command, args);
            return true;
        });
    }

    // A misconfigured command or a failed launch is reported, not thrown: it must not crash the host.
    private ShellResult Launch(string what, Func<bool> start)
    {
        try
        {
            return start() ? ShellResult.Ok : ShellResult.Failed($"{what} failed");
        }
        catch (Win32Exception ex)
        {
            _logger?.Warning("{Action} failed: {Exception}", what, ex.Message);
            return ShellResult.Failed($"{what} failed: {ex.Message}");
        }
    }

    // ShowFileProperties from http://stackoverflow.com/a/1936957
    // SYSLIB1054 suppressed: SHELLEXECUTEINFO is non-blittable (string fields), so LibraryImport cannot marshal it
    // automatically, and the generator needs AllowUnsafeBlocks (not enabled in cdeAppCore). This is a rare,
    // user-initiated UI call, not a hot path, so the runtime-marshalled DllImport is kept.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute'",
        Justification = "Non-blittable SHELLEXECUTEINFO struct; not a hot path; cdeAppCore has no AllowUnsafeBlocks.")]
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;

        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpClass;

        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    // ReSharper disable once InconsistentNaming
    private const int SW_SHOW = 5;

    // ReSharper disable once InconsistentNaming
    private const uint SEE_MASK_INVOKEIDLIST = 12;
}
