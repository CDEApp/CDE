using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace cdeWin;

internal static class Program
{
    public static IConfigurationRoot Configuration;

    public static string Version => Application.ProductVersion;

    public static string ProductName => Application.ProductName;

    [STAThread]
    private static void Main()
    {
        LoggingBootstrap.CreateLogger();
        Application.ThreadException += UIThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // TODO consider using (var config = new Config()) { } - with Save built in.
        var config = new Config("cdeWinView.cfg", ProductName, Version);
        var mainForm = new CDEWinForm(config);
        var mainPresenter = new CDEWinFormPresenter(mainForm, config, new LoadCatalogService(Log.Logger));
        config.RestoreConfigFormBase(mainForm);
        config.RestoreConfig(mainForm); // after presenter is configured and wired up events.
        LoadAppSettingsConfig();
        Application.Run(mainForm);
        SaveAppState(config, mainForm);
    }

    private static void SaveAppState(Config config, CDEWinForm mainForm)
    {
        config.Active.MainWindowConfig.RecordForm(mainForm);
        config.Save();
    }

    private static void LoadAppSettingsConfig()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
            .AddJsonFile("appsettings.json", false)
            .Build();
    }

    private static void UIThreadException(object sender, ThreadExceptionEventArgs t)
    {
        var result = DialogResult.Cancel;

        try
        {
            MessageBox.Show("OnThreadException: " + t.Exception, "Thread Exception Caught");
            result = MessageBox.Show("UIThreadException caught", "Error", MessageBoxButtons.AbortRetryIgnore);
        }
        catch
        {
            Application.Exit();
        }

        // Exits the program when the user clicks Abort.
        if (result == DialogResult.Abort)
        {
            Application.Exit();
        }
    }

    private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show("CurrentDomain_UnhandledException: " + ex, "Unhandled Exception Caught");
            MessageBox.Show("CurrentDomainUnhandledException caught", "Error", MessageBoxButtons.OK);
        }
        finally
        {
            Application.Exit();
        }
    }
}