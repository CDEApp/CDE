using System;
using System.Threading;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    static class Program
    {
		public static string Version
		{
			get { return Application.ProductVersion; }
		}

		public static string ProductName
		{
			get { return Application.ProductName; }
		}

        [STAThread]
        static void Main()
        {
            Application.ThreadException += UIThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            // TODO consider using (var config = new Config()) { } - with Save built in.
			var config = new Config("cdeWinView.cfg", ProductName, Version);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var rootEntries = RootEntry.LoadMultiDirCacheWithChildren(new[] { ".", config.ConfigPath });

            var mainForm = new CDEWinForm(config);
            var mainPresenter = new CDEWinFormPresenter(mainForm, rootEntries, config);
            config.Active.MainWindowConfig.RestoreForm(mainForm);
            config.RestoreConfig(mainForm); // after presenter is configured and wired up events.
            //mainPresenter.Display();
            Application.Run(mainForm);

            config.Active.MainWindowConfig.RecordForm(mainForm);
            config.Save();
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
}
