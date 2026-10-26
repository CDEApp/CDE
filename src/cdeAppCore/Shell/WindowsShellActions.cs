using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

    public void Open(string fullPath)
    {
        var p = new Process { StartInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true } };
        p.Start();
    }

    public void Explore(string fullPath)
    {
        Process.Start("explorer.exe", "/select,\"" + fullPath + "\"");
    }

    public void ShowProperties(string fullPath)
    {
        var info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = fullPath;
        info.nShow = SW_SHOW;
        info.fMask = SEE_MASK_INVOKEIDLIST;
        ShellExecuteEx(ref info);
    }

    public void RunCustomCommand(int commandIndex, string fullPath)
    {
        if (commandIndex < 0 || commandIndex >= CustomCommands.Count) return;
        RunCustomCommand(CustomCommands[commandIndex], fullPath);
    }

    public void RunCustomCommand(CustomCommandOptions command, string fullPath)
    {
        if (command == null || string.IsNullOrEmpty(command.Command)) return;
        var args = CommandTokens.Substitute(command.Arguments ?? "", fullPath);
        try
        {
            Process.Start(command.Command, args);
        }
        catch (Win32Exception ex)
        {
            // Log and continue — a misconfigured custom command must not crash the host.
            _logger?.Warning("RunCustomCommand failed for {Command}: {Exception}", command.Command, ex.Message);
        }
    }

    // ShowFileProperties from http://stackoverflow.com/a/1936957
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
