using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace cdeWin
{
    public static class WindowsExplorerUtilities
    {
        // ShowFileProperties from http://stackoverflow.com/a/1936957
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHELLEXECUTEINFO
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

        private const int SW_SHOW = 5;
        private const uint SEE_MASK_INVOKEIDLIST = 12;

        public static void ShowFileProperties(string Filename)
        {
            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = Filename;
            info.nShow = SW_SHOW;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            ShellExecuteEx(ref info);
        }

        public static void ExplorerOpen(string path)
        {
            var p = new Process {StartInfo = new ProcessStartInfo(path) {UseShellExecute = true}};
            p.Start();
        }

        public static void ExplorerExplore(string path)
        {
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        public static void TotalCommanderExplore(string path)
        {
            var explorerAltConfig = new ExplorerAltOptions();
            Program.Configuration.GetSection("ExplorerAlt").Bind(explorerAltConfig);
            var execPath = explorerAltConfig.Path;
            if (string.IsNullOrEmpty(execPath))
            {
                //Registry.CurrentUser.OpenSubKey("Software\\Ghisler\\Total Commander");
                //var installDir = Registry.GetValue(keyName, valueName, "default");
                execPath = "C:\\program files\\totalcmd\\totalcmd64.exe";
            }

            Process.Start(execPath, "/O /T /R=" + path);
        }
    }

    public class ExplorerAltOptions
    {
        public string Path { get; set; }
    }
}