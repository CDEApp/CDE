using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Util;
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
	        var timeIt = new TimeIt();
            Application.ThreadException += UIThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

			// TODO consider using (var config = new Config()) { } - with Save built in.
			var config = new Config("cdeWinView.cfg", ProductName, Version);
	        var cachePathList = new[] {".", config.ConfigPath};
			var loaderForm = new LoaderForm(config, cachePathList, timeIt);

	        List<RootEntry> rootEntries = null;

			try
			{
				//var cacheFiles = RootEntry.GetCacheFileList(cachePathList);
				//var cacheFiles = RootEntry.GetCacheFileList(cachePathList);
				//rootEntries = RootEntry.Load(cacheFiles);
				loaderForm.AutoCloseLoaderFlag = config.Active.AutoCloseLoader;
				loaderForm.ShowDialog();
			}
			finally
			{
				config.Active.AutoCloseLoader = loaderForm.AutoCloseLoaderFlag;
				rootEntries = loaderForm.RootEntries;
				loaderForm.Dispose();
			}


			//var ArootEntries = RootEntry.LoadMultiDirCacheWithChildren(cachePathList);

            var mainForm = new CDEWinForm(config);
            var mainPresenter = new CDEWinFormPresenter(mainForm, rootEntries, config);
			config.RestoreConfigFormBase(mainForm);
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
